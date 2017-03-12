﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Elasticsearch.Net;
using Nest;
using static ElasticUp.Operation.Validator.OperationValidator;

namespace ElasticUp.Operation.Reindex
{
    public class BatchUpdateOperation<TTransformFromType, TTransformToType> : AbstractElasticUpOperation
                                                                                where TTransformFromType : class
                                                                                where TTransformToType : class
    {
        private readonly BatchUpdateArguments<TTransformFromType, TTransformToType> _arguments;

        public BatchUpdateOperation(Func<BatchUpdateDescriptor<TTransformFromType, TTransformToType>, BatchUpdateArguments<TTransformFromType, TTransformToType>> arguments)
        {
            _arguments = arguments.Invoke(new BatchUpdateDescriptor<TTransformFromType, TTransformToType>());
        }

        public override void Validate(IElasticClient elasticClient)
        {
            var validate = ValidatorFor<BatchUpdateOperation<TTransformFromType, TTransformToType>>(elasticClient);

            validate.IsNotBlank(_arguments.FromIndexName, RequiredMessage("FromIndexName"));
            validate.IsNotBlank(_arguments.ToIndexName, RequiredMessage("ToIndexName"));
            validate.IsNotBlank(_arguments.FromTypeName, RequiredMessage("FromTypeName"));
            validate.IsNotBlank(_arguments.ToTypeName, RequiredMessage("ToTypeName"));
            validate.IndexExists(_arguments.FromIndexName);
            validate.IndexExists(_arguments.ToIndexName);
        }

        public override void Execute(IElasticClient elasticClient)
        {
            //TODO reset Threadpool after processing?
            if (_arguments.DegreeOfParallellism > 1) ThreadPool.SetMaxThreads(_arguments.DegreeOfParallellism,_arguments.DegreeOfParallellism);


            var searchResponse = Search(elasticClient);
            if (!searchResponse.Documents.Any()) return;

            var tasks = new List<Task>();

            do
            {
                var hits = searchResponse.Hits;

                if (_arguments.DegreeOfParallellism > 1) { tasks.Add(Task.Run(() => ProcessHits(elasticClient, hits))); }
                else { ProcessHits(elasticClient, hits); }
                searchResponse = elasticClient.Scroll<TTransformFromType>(_arguments.ScrollTimeout, searchResponse.ScrollId);
            }
            while (searchResponse.Documents.Any());

            Task.WaitAll(tasks.ToArray());
        }

        protected ISearchResponse<TTransformFromType> Search(IElasticClient elasticClient)
        {
            return elasticClient
                .Search<TTransformFromType>(descriptor => _arguments.SearchDescriptor(descriptor
                    .Index(_arguments.FromIndexName)
                    .Type(_arguments.FromTypeName)
                    .Version()
                    .Scroll(_arguments.ScrollTimeout)
                    .Size(_arguments.BatchSize)));
        }

        protected virtual void ProcessHits(IElasticClient elasticClient, IEnumerable<IHit<TTransformFromType>> hits)
        {
            var transformedDocuments = TransformDocuments(hits).ToList();
            BulkIndex(elasticClient, transformedDocuments);
            transformedDocuments.ForEach(doc => _arguments.OnDocumentProcessed?.Invoke(doc.TransformedDocment));
        }

        protected void BulkIndex(IElasticClient elasticClient, IEnumerable<TransformedDocument<TTransformFromType, TTransformToType>> transformedDocuments)
        {
            var bulkDescriptor = new BulkDescriptor();

            foreach (var document in transformedDocuments)
            {
                if (document.TransformedDocment == null)
                    continue;

                if (document.Hit.Version.HasValue)
                {
                    bulkDescriptor.Index<object>(
                        descr => descr
                            .Index(_arguments.ToIndexName)
                            .Id(document.Hit.Id)
                            .Type(_arguments.ToTypeName)
                            .VersionType(VersionType.External)
                            .Version(document.Hit.Version)
                            .Document(document.TransformedDocment));
                }
                else
                {
                    bulkDescriptor.Index<object>(
                        descr => descr
                            .Index(_arguments.ToIndexName)
                            .Id(document.Hit.Id)
                            .Type(_arguments.ToTypeName)
                            .Document(document.TransformedDocment));
                }
            }

            elasticClient.Bulk(bulkDescriptor);
        }

        protected IEnumerable<TransformedDocument<TTransformFromType, TTransformToType>> TransformDocuments(IEnumerable<IHit<TTransformFromType>> hits)
        {
            return hits
                .Where(hit => hit.Source != null)
                .Select(hit => new TransformedDocument<TTransformFromType, TTransformToType>
                {
                    Hit = hit,
                    TransformedDocment = _arguments.Transformation(hit.Source)
                });
        }
    }
}