﻿using System;
using Elasticsearch.Net;

namespace ElasticUp.Util
{
    public class ElasticUpException : Exception
    {
        public ServerError ServerError { get; private set; }
        public string DebugInformation { get; private set;  }

        public ElasticUpException(string message) : base(message) { }

        public ElasticUpException(string message, Exception innerException) : base(message, innerException) { }

        public ElasticUpException(string message, ServerError serverError, string debugInformation) : base(message)
        {
            ServerError = serverError;
            DebugInformation = debugInformation;
        }

        public override string ToString()
        {
            return $"ElasticUpException: {Message}, DEBUGINFORMATION: {DebugInformation}";
        }
    }
}
