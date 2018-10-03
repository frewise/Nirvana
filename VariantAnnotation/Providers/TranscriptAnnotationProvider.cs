﻿using System.Collections.Generic;
using System.IO;
using System.Linq;
using ErrorHandling.Exceptions;
using Genome;
using Intervals;
using IO;
using OptimizedCore;
using VariantAnnotation.AnnotatedPositions;
using VariantAnnotation.Caches;
using VariantAnnotation.Interface.AnnotatedPositions;
using VariantAnnotation.Interface.Caches;
using VariantAnnotation.Interface.Providers;
using VariantAnnotation.IO.Caches;
using VariantAnnotation.TranscriptAnnotation;
using Variants;

namespace VariantAnnotation.Providers
{
    public sealed class TranscriptAnnotationProvider : ITranscriptAnnotationProvider
    {
        private const int MaxSvLengthForRegulatoryRegionAnnotation = 50000;

        private readonly ITranscriptCache _transcriptCache;
        private readonly ISequence _sequence;

	    public string Name { get; }
	    public GenomeAssembly Assembly { get; }
        public IEnumerable<IDataSourceVersion> DataSourceVersions { get; }
        public ushort VepVersion { get; }

        private readonly PredictionCacheReader _siftReader;
        private readonly PredictionCacheReader _polyphenReader;
        private IPredictionCache _siftCache;
        private IPredictionCache _polyphenCache;
        private ushort _currentRefIndex = ushort.MaxValue;

        public TranscriptAnnotationProvider(string pathPrefix,  ISequenceProvider sequenceProvider)
        {
            Name      = "Transcript annotation provider";
            _sequence = sequenceProvider.Sequence;

            (_transcriptCache, VepVersion) = InitiateCache(FileUtilities.GetReadStream(CacheConstants.TranscriptPath(pathPrefix)),
                sequenceProvider.RefIndexToChromosome, sequenceProvider.Assembly);

            Assembly           = _transcriptCache.Assembly;
            DataSourceVersions = _transcriptCache.DataSourceVersions;

            _siftReader     = new PredictionCacheReader(FileUtilities.GetReadStream(CacheConstants.SiftPath(pathPrefix)),     PredictionCacheReader.SiftDescriptions);
            _polyphenReader = new PredictionCacheReader(FileUtilities.GetReadStream(CacheConstants.PolyPhenPath(pathPrefix)), PredictionCacheReader.PolyphenDescriptions);
        }

        private static (TranscriptCache cache, ushort vepVersion) InitiateCache(Stream stream,
            IDictionary<ushort, IChromosome> refIndexToChromosome, GenomeAssembly refAssembly)
        {
            TranscriptCache cache;
            ushort vepVersion;

            using (var reader = new TranscriptCacheReader(stream))
            {
                vepVersion = reader.Header.Custom.VepVersion;
                CheckHeaderVersion(reader.Header, refAssembly);
                cache = reader.Read(refIndexToChromosome).GetCache();
            }

            return (cache, vepVersion);
        }

        private static void CheckHeaderVersion(Header header, GenomeAssembly refAssembly)
        {
            if (header.Assembly != refAssembly)
                throw new UserErrorException(GetAssemblyErrorMessage(header.Assembly, refAssembly));

            if (header.SchemaVersion != CacheConstants.SchemaVersion)
                throw new UserErrorException(
                    $"Expected the cache schema version ({CacheConstants.SchemaVersion}) to be identical to the schema version in the cache header ({header.SchemaVersion})");
        }

        private static string GetAssemblyErrorMessage(GenomeAssembly cacheAssembly, GenomeAssembly refAssembly)
        {
            var sb = StringBuilderCache.Acquire();
            sb.AppendLine("Not all of the data sources have the same genome assembly:");
            sb.AppendLine($"- Using {refAssembly}: Reference sequence provider");
            sb.AppendLine($"- Using {cacheAssembly}: Transcript annotation provider");
            return StringBuilderCache.GetStringAndRelease(sb);
        }

        public void Annotate(IAnnotatedPosition annotatedPosition)
        {
            if (annotatedPosition.AnnotatedVariants == null || annotatedPosition.AnnotatedVariants.Length == 0) return;

            var refIndex = annotatedPosition.Position.Chromosome.Index;
            LoadPredictionCaches(refIndex);

            AddRegulatoryRegions(annotatedPosition);
            AddTranscripts(annotatedPosition);
        }

        public void PreLoad(IChromosome chromosome, List<int> positions)
        {
            throw new System.NotImplementedException();
        }

        private void LoadPredictionCaches(ushort refIndex)
        {
            if (refIndex == _currentRefIndex) return;
            if (refIndex == ushort.MaxValue)
            {
                ClearCache();
                return;
            }
            _siftCache       = _siftReader.Read(refIndex);
            _polyphenCache   = _polyphenReader.Read(refIndex);
            _currentRefIndex = refIndex;
        }

        private void ClearCache()
        {
            _siftCache = null;
            _polyphenCache = null;
            _currentRefIndex = ushort.MaxValue; 
        }

        private void AddTranscripts(IAnnotatedPosition annotatedPosition)
        {
            var overlappingTranscripts = _transcriptCache.GetOverlappingTranscripts(annotatedPosition.Position);

            if (overlappingTranscripts == null) return;

            foreach (var annotatedVariant in annotatedPosition.AnnotatedVariants)
            {
                var geneFusionCandidates = GetGeneFusionCandiates(annotatedVariant.Variant.BreakEnds);
                var annotatedTranscripts = new List<IAnnotatedTranscript>();

                TranscriptAnnotationFactory.GetAnnotatedTranscripts(annotatedVariant.Variant, overlappingTranscripts,
                    _sequence, annotatedTranscripts, annotatedVariant.OverlappingGenes,
                    annotatedVariant.OverlappingTranscripts,_siftCache,_polyphenCache, geneFusionCandidates);

                if (annotatedTranscripts.Count == 0) continue;

                foreach (var annotatedTranscript in annotatedTranscripts)
                {
                    if (annotatedTranscript.Transcript.Source == Source.Ensembl)
                        annotatedVariant.EnsemblTranscripts.Add(annotatedTranscript);
                    else annotatedVariant.RefSeqTranscripts.Add(annotatedTranscript);
                }
            }
        }

        private ITranscript[] GetGeneFusionCandiates(IBreakEnd[] breakEnds)
        {
            if (breakEnds == null || breakEnds.Length == 0) return null;

            var geneFusionCandidates = new HashSet<ITranscript>();
            foreach (var breakEnd in breakEnds)
            {
                var candiates = _transcriptCache.GetOverlappingTranscripts(breakEnd.Piece2.Chromosome,
                    breakEnd.Piece2.Position, breakEnd.Piece2.Position);
                if (candiates == null) continue;
                foreach (var candiate in candiates) geneFusionCandidates.Add(candiate);
            }

            return geneFusionCandidates.ToArray();
        }

        private void AddRegulatoryRegions(IAnnotatedPosition annotatedPosition)
        {
            var overlappingRegulatoryRegions = _transcriptCache.GetOverlappingRegulatoryRegions(annotatedPosition.Position);

            if (overlappingRegulatoryRegions == null) return;

            foreach (var annotatedVariant in annotatedPosition.AnnotatedVariants)
            {
                // In case of insertions, the base(s) are assumed to be inserted at the end position

                // if this is an insertion just before the beginning of the regulatory element, this takes care of it
                var variant      = annotatedVariant.Variant;
                var variantEnd   = variant.End;
                var variantBegin = variant.Type == VariantType.insertion ? variant.End : variant.Start;

                // disable regulatory region for SV larger than 50kb
                if (variantEnd - variantBegin + 1 > MaxSvLengthForRegulatoryRegionAnnotation) continue;

                foreach (var regulatoryRegion in overlappingRegulatoryRegions)
                {
                    if (!variant.Overlaps(regulatoryRegion)) continue;

                    // if the insertion is at the end, its past the feature and therefore not overlapping
                    if (variant.Type == VariantType.insertion && variantEnd == regulatoryRegion.End) continue;

                    annotatedVariant.RegulatoryRegions.Add(RegulatoryRegionAnnotator.Annotate(variant, regulatoryRegion));
                }
            }
        }
    }
}