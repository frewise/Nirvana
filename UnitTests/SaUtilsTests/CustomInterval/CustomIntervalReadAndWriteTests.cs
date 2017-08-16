﻿using System.Collections.Generic;
using System.Globalization;
using UnitTests.Utilities;
using Xunit;

namespace UnitTests.SaUtilsTests.CustomInterval
{
    public sealed class CustomIntervalReadAndWriteTests : RandomFileBase
    {
        #region members

        private readonly int _totalIntervals = 3;
        private readonly List<SAUtils.DataStructures.CustomInterval> _expectedCustomIntervals = new List<SAUtils.DataStructures.CustomInterval>();

        #endregion

        public CustomIntervalReadAndWriteTests()
        {
            var intervalType = "TestInterval";

            for (var i = 0; i < _totalIntervals; i++)
            {
                var start = 100 + i * 10000;
                var end = 200 + i * 100 + i * 10000;

                var geneName = "TestGene" + i;
                var evidence = "Class" + i;
                var length = end - start + 1;
                // ReSharper disable once PossibleLossOfFraction
                float score = i * 3 / 5;

                var stringValues = new Dictionary<string, string>();
                var nonstringValues = new Dictionary<string, string>();

                stringValues.Add("Gene", geneName);
                stringValues.Add("Evidence", evidence);
                nonstringValues.Add("score", score.ToString(CultureInfo.InvariantCulture));
                nonstringValues.Add("length", length.ToString());

                if (i == 0)
                {
                    var customInterval = new SAUtils.DataStructures.CustomInterval("chr1", start, end, intervalType,
                        null, null);
                    _expectedCustomIntervals.Add(customInterval);
                }
                else
                {
                    var customInterval = new SAUtils.DataStructures.CustomInterval("chr1", start, end, intervalType,
                stringValues, nonstringValues);
                    _expectedCustomIntervals.Add(customInterval);
                }
            }

            //var randomPath = GetRandomPath();
            //WriteCustomIntervalFile(randomPath);
        }

        //private void WriteCustomIntervalFile(string filePath)
        //{
        //    var dataVersion = new DataSourceVersion("customInterval", "00", DateTime.Now.Ticks);
        //    using (var writer = new CustomIntervalWriter(filePath, "chr1", _intervalType, dataVersion))
        //    {
        //        for (var i = 0; i < _totalIntervals; i++)
        //        {
        //            writer.WriteInterval(_expectedCustomIntervals[i]);
        //        }
        //    }
        //}

        [Fact(Skip = "wait to change to tsv writer tests")]
        public void EndofFile()
        {
            //// read the supplementary annotation file
            //using (var reader = new CustomIntervalReader(_randomPath))
            //{
            //    for (var i = 0; i < _totalIntervals; i++)
            //    {
            //        reader.GetNextCustomInterval();
            //    }

            //    var observerdInterval = reader.GetNextCustomInterval();

            //    Assert.Null(observerdInterval);

            //    var observerdInterval2 = reader.GetNextCustomInterval();

            //    Assert.Null(observerdInterval2);
            //}
        }

        [Fact(Skip = "wait to change to tsv writer tests")]
        public void ReadAndWriteTests()
        {
            //using (var reader = new CustomIntervalReader(_randomPath))
            //{
            //    var observedType = reader.GetIntervalType();
            //    var observedRefName = reader.GetReferenceName();

            //    Assert.Equal(_intervalType, observedType);
            //    Assert.Equal("chr1", observedRefName);

            //    for (var i = 0; i < _totalIntervals; i++)
            //    {
            //        var observedInterval = reader.GetNextCustomInterval();

            //        Assert.Equal(_expectedCustomIntervals[i].ReferenceName, observedInterval.ReferenceName);
            //        Assert.Equal(_expectedCustomIntervals[i].Start, observedInterval.Start);
            //        Assert.Equal(_expectedCustomIntervals[i].End, observedInterval.End);

            //        if (i > 0)
            //        {
            //            Assert.Equal(_expectedCustomIntervals[i].StringValues, observedInterval.StringValues);
            //            Assert.Equal(_expectedCustomIntervals[i].NonStringValues, observedInterval.NonStringValues);
            //        }
            //        else
            //        {
            //            Assert.Null(observedInterval.StringValues);
            //            Assert.Null(observedInterval.NonStringValues);
            //        }
            //    }
            //}
        }

        [Fact(Skip = "wait to change to tsv writer tests")]
        public void DifferentRefNameException()
        {
            //var customInterval = new VariantAnnotation.DataStructures.CustomInterval("chr2", 100, 200, _intervalType,
            //    null, null);
            //var randomPath = GetRandomPath();

            //var dataVersion = new DataSourceVersion("customInterval", "00", DateTime.Now.Ticks);
            //using (var writer = new CustomIntervalWriter(randomPath, "chr1", _intervalType, dataVersion))
            //{
            //    // ReSharper disable once AccessToDisposedClosure
            //    var ex = Assert.Throws<Exception>(() => writer.WriteInterval(customInterval));

            //    Assert.Equal("Unexpected interval in custom interval writer.\nExpected reference name: chr1, observed reference name: chr2", ex.Message);
            //}
        }

        [Fact(Skip = "wait to change to tsv writer tests")]
        public void DifferentTypeException()
        {
            //var customInterval = new VariantAnnotation.DataStructures.CustomInterval("chr1", 100, 200, "WrongType",
            //    null, null);
            //var randomPath = GetRandomPath();

            //var dataVersion = new DataSourceVersion("customInterval", "00", DateTime.Now.Ticks);
            //using (var writer = new CustomIntervalWriter(randomPath, "chr1", _intervalType, dataVersion))
            //{
            //    // ReSharper disable once AccessToDisposedClosure
            //    var ex = Assert.Throws<Exception>(() => writer.WriteInterval(customInterval));

            //    Assert.Equal($"Unexpected interval in custom interval writer.\nExpected interval type: {_intervalType}, observed interval type: WrongType", ex.Message);
            //}
        }
    }
}