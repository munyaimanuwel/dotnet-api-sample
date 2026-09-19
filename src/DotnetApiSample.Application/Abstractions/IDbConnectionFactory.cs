using System.Data;

namespace DotnetApiSample.Application.Abstractions;

public interface IDbConnectionFactory
{
    IDbConnection CreateConnection();
}
