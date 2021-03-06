﻿using System.Collections.Generic;
using System.IO;
using IO;
using OptimizedCore;
using VariantAnnotation.Utilities;

namespace CacheUtils.IntermediateIO
{
    public static class CcdsReader
    {
        private const int CcdsIdIndex       = 0;
        private const int NucleotideIdIndex = 4;

        public static Dictionary<string, List<string>> GetCcdsIdToEnsemblId(string ccdsPath)
        {
            var ccdsIdToEnsemblId = new Dictionary<string, List<string>>();

            using (var reader = FileUtilities.GetStreamReader(FileUtilities.GetReadStream(ccdsPath)))
            {
                while (true)
                {
                    string line = reader.ReadLine();
                    if (line == null) break;
                    if (line.OptimizedStartsWith('#')) continue;

                    var cols = line.OptimizedSplit('\t');
                    if (cols.Length != 8) throw new InvalidDataException($"Expected 8 columns, but found {cols.Length}: [{line}]");

                    string nucleotideId = cols[NucleotideIdIndex];
                    if (!nucleotideId.StartsWith("ENST")) continue;

                    var ccds    = FormatUtilities.SplitVersion(cols[CcdsIdIndex]);
                    var ensembl = FormatUtilities.SplitVersion(nucleotideId);

                    if (ccdsIdToEnsemblId.TryGetValue(ccds.Id, out var ensemblList)) ensemblList.Add(ensembl.Id);
                    else ccdsIdToEnsemblId[ccds.Id] = new List<string> { ensembl.Id };
                }
            }

            return ccdsIdToEnsemblId;
        }
    }
}
