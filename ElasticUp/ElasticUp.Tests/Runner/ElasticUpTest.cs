
using System;
using ElasticUp.Tests.Migration;
using Nest;
using NUnit.Framework;

namespace ElasticUp.Tests.Runner
{
    [TestFixture]
    public class ElasticUpTest
    {
        [Test]
        public void WhenAddingAMigration_MakeSureAllMigrationNumbersAreUnique()
        {
            Assert.Throws<ArgumentException>(() =>
                ElasticUp.Runner.ElasticUp.ConfigureElasticUp(new ElasticClient())
                    .Migration(new TestMigration(1))
                    .Migration(new TestMigration(1)));
        }

        [Test]
        public void WhenAddingAMigration_MakeSureAllMigrationNumbersAreAscending()
        {
            Assert.Throws<ArgumentException>(() =>
                ElasticUp.Runner.ElasticUp.ConfigureElasticUp(new ElasticClient())
                    .Migration(new TestMigration(2))
                    .Migration(new TestMigration(1)));
        }
    }
}