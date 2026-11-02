using System;
using System.Diagnostics.CodeAnalysis;

namespace DCH.CsvIntakeAgent.Exceptions;

[ExcludeFromCodeCoverage]
public class CsvIntakeAgentException : Exception
{
    public CsvIntakeAgentException(string message)
        : base(message)
    {
    }

    public CsvIntakeAgentException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}