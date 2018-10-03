﻿using System.Collections.Generic;
using Genome;
using Nirvana;
using VariantAnnotation;
using VariantAnnotation.Interface.AnnotatedPositions;
using VariantAnnotation.Interface.Positions;
using VariantAnnotation.Interface.Providers;
using Vcf;
using Vcf.VariantCreator;

namespace UnitTests.TestUtilities
{
    public static class AnnotationUtilities
	{
        internal static IAnnotatedPosition GetAnnotatedPosition(string cacheFilePrefix, List<string> saPaths,
            string vcfLine, bool enableVerboseTranscripts, IDictionary<string, IChromosome> refNameToChromosome)
        {
            var refMinorProvider = ProviderUtilities.GetRefMinorProvider(saPaths);
            var annotatorAndRef = GetAnnotatorAndReferenceDict(cacheFilePrefix, saPaths);

            var annotator      = annotatorAndRef.Annotator;
            var refNames       = annotatorAndRef.RefNames;
            var variantFactory = new VariantFactory(refNames, enableVerboseTranscripts);

            var position          = ParseVcfLine(vcfLine, refMinorProvider, variantFactory, refNames);
            var annotatedPosition = annotator.Annotate(position);

            return annotatedPosition;
        }

	    internal static IPosition ParseVcfLine(string vcfLine, IRefMinorProvider refMinorProvider, VariantFactory variantFactory, IDictionary<string, IChromosome> refNameToChromosome)
	    {
	        var simplePosition = SimplePosition.GetSimplePosition(vcfLine, refNameToChromosome);
	        return Position.ToPosition(simplePosition, refMinorProvider, variantFactory);
	    }

        private static (Annotator Annotator, IDictionary<string, IChromosome> RefNames) GetAnnotatorAndReferenceDict(string cacheFilePrefix, List<string> saPaths)
        {
            var sequenceFilePath             = cacheFilePrefix + ".bases";
            var sequenceProvider             = ProviderUtilities.GetSequenceProvider(sequenceFilePath);
            var refNames                     = sequenceProvider.RefNameToChromosome;
            var transcriptAnnotationProvider = ProviderUtilities.GetTranscriptAnnotationProvider(cacheFilePrefix, sequenceProvider);
            var saProvider                   = ProviderUtilities.GetNsaProvider(saPaths);
            var conservationProvider         = ProviderUtilities.GetConservationProvider(saPaths);

            var annotator = new Annotator(transcriptAnnotationProvider, sequenceProvider, saProvider, conservationProvider, null);
            return (annotator,refNames);
        }
    }
}