﻿using System;
using System.Collections.Generic;
using System.IO;
using CommandLine.Builders;
using CommandLine.NDesk.Options;
using Compression.Utilities;
using ErrorHandling;
using IO;
using SAUtils.DataStructures;
using VariantAnnotation.Interface.SA;
using VariantAnnotation.NSA;
using VariantAnnotation.Providers;
using VariantAnnotation.SA;

namespace SAUtils.Custom
{
    public static class Main
    {
        private static string _inputFile;
        private static string _compressedReference;
        private static string _outputDirectory;
        public static ExitCodes Run(string command, string[] commandArgs)
        {
            var ops = new OptionSet
            {
                {
                     "ref|r=",
                     "compressed reference sequence file",
                     v => _compressedReference = v
                 },
                {
                    "in|i=",
                    "custom TSV file path",
                    v => _inputFile = v
                },
                {
                    "out|o=",
                    "output directory",
                    v => _outputDirectory = v
                }
            };

            string commandLineExample = $"{command} [options]";

            var exitCode = new ConsoleAppBuilder(commandArgs, ops)
                .Parse()
                .CheckInputFilenameExists(_compressedReference, "compressed reference sequence file name", "--ref")
                .HasRequiredParameter(_inputFile, "Custom TSV file", "--in")
                .CheckInputFilenameExists(_inputFile, "Custom TSV file", "--in")
                .HasRequiredParameter(_outputDirectory, "output directory", "--out")
                .CheckDirectoryExists(_outputDirectory, "output directory", "--out")
                .SkipBanner()
                .ShowHelpMenu("Creates a supplementary database from a custom input file", commandLineExample)
                .ShowErrors()
                .Execute(ProgramExecution);

            return exitCode;
        }

        private static ExitCodes ProgramExecution()
        {
            var referenceProvider = new ReferenceSequenceProvider(FileUtilities.GetReadStream(_compressedReference));
            
            List<CustomInterval> intervals;
            var jsonTag = "";
            DataSourceVersion version;
            string outputPrefix = GetOutputPrefix(_inputFile);
            
            using (var customReader = new CustomAnnotationsParser(GZipUtilities.GetAppropriateStreamReader(_inputFile), referenceProvider.RefNameToChromosome))
            using (var nsaStream   = FileUtilities.GetCreateStream(Path.Combine(_outputDirectory, outputPrefix + SaCommon.SaFileSuffix)))
            using (var indexStream = FileUtilities.GetCreateStream(Path.Combine(_outputDirectory, outputPrefix + SaCommon.SaFileSuffix + SaCommon.IndexSufix)))
            using (var nsaWriter   = new NsaWriter(new ExtendedBinaryWriter(nsaStream), new ExtendedBinaryWriter(indexStream), 
                version = new DataSourceVersion(customReader.JsonTag, GetInputFileName(_inputFile), DateTime.Now.Ticks), 
                referenceProvider, customReader.JsonTag, true, false, SaCommon.SchemaVersion, false))
            {
                jsonTag = customReader.JsonTag;
                nsaWriter.Write(customReader.GetItems());
                intervals = customReader.GetCustomIntervals();
            }

            if (intervals == null) return ExitCodes.Success;
            
            using (var nsiStream = FileUtilities.GetCreateStream(Path.Combine(_outputDirectory, outputPrefix + SaCommon.SiFileSuffix)))
            using (var nsiWriter = new NsiWriter(new ExtendedBinaryWriter(nsiStream), version, referenceProvider.Assembly, jsonTag, ReportFor.AllVariants, SaCommon.SchemaVersion))
            {
                nsiWriter.Write(intervals);
            }

            return ExitCodes.Success;
        }

        private static string GetOutputPrefix(string inputFilePath)
        {
            var fileName = GetInputFileName(inputFilePath);
            if (fileName.EndsWith(".tsv"))
                return fileName.Substring(0, fileName.Length - 4);
            if (fileName.EndsWith(".tsv.gz"))
                return fileName.Substring(0, fileName.Length - 7);

            return fileName;
        }

        private static string GetInputFileName(string inputFilePath)
        {
            var fileNameIndex = inputFilePath.LastIndexOf(Path.DirectorySeparatorChar);
            if (fileNameIndex < 0) return inputFilePath;
            return inputFilePath.Substring(fileNameIndex + 1);

        }
    }

}