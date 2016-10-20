﻿using System;
using ElasticUp.History;
using ElasticUp.Migration.Meta;
using ElasticUp.Operation;
using ElasticUp.Tests.Sample;
using FluentAssertions;
using Nest;
using NUnit.Framework;

namespace ElasticUp.Tests.Operation
{
    [TestFixture]
    public class CopyTypeOperationIntegrationTest : AbstractIntegrationTest
    {
        public CopyTypeOperationIntegrationTest() : base(ElasticServiceStartupType.StartupForEach)
        {
        }

        [Test]
        public void Execute_CopiesTypeToNewIndex()
        {
            // GIVEN
            var oldIndex = new VersionedIndexName("test", 0);
            var newIndex = oldIndex.GetIncrementedVersion();

            ElasticClient.IndexMany(new[] { new SampleDocument() }, oldIndex.ToString());
            ElasticClient.Refresh(Indices.All);

            // WHEN
            var operation = new CopyTypeOperation<SampleDocument>(0);
            operation.Execute(ElasticClient, oldIndex, newIndex);

            // THEN
            ElasticClient.Refresh(Indices.All);
            var countResponse = ElasticClient.Count<SampleDocument>(descriptor => descriptor.Index(newIndex.ToString()));
            countResponse.Count.Should().Be(1);
        }

        [Test]
        public void Execute_ThrowsWhenFromIndexDoesNotExist()
        {
            // GIVEN
            var oldIndex = new VersionedIndexName("this-index-does-not-exist", 0);
            var newIndex = oldIndex.GetIncrementedVersion();

            // WHEN
            var operation = new CopyTypeOperation<SampleDocument>(0);
            Assert.Throws<Exception>(() => operation.Execute(ElasticClient, oldIndex, newIndex));
        }

        [Test]
        public void Execute_DoesNotThrowWhenNoDocumentsOfTypeAvailableInFromIndex()
        {
            // GIVEN
            var oldIndex = new VersionedIndexName("test", 0);
            var newIndex = oldIndex.GetIncrementedVersion();

            ElasticClient.CreateIndex(oldIndex.ToString());
            ElasticClient.Refresh(Indices.All);

            // WHEN
            var operation = new CopyTypeOperation<SampleDocument>(0);
            operation.Execute(ElasticClient, oldIndex, newIndex);

            // THEN
            ElasticClient.Refresh(Indices.All);
            var countResponse = ElasticClient.Count<SampleDocument>(descriptor => descriptor.Index(newIndex.ToString()));
            countResponse.Count.Should().Be(0);
        }
    }
}