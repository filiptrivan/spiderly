using System;
using System.Collections.Generic;
using System.Text;

namespace Spiderly.SourceGenerators.Shared
{
    public class SpiderlySourceGeneratorException : Exception
    {
        public string MethodName { get;  }

        public SpiderlySourceGeneratorException(string message, string methodName)
             : base(message)
        {
            MethodName = methodName;
        }

    }
}
