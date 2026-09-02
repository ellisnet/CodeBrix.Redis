using System.Text;

namespace CodeBrix.Redis; //was previously: StackExchange.Redis;

internal interface ICompletable
{
    void AppendStormLog(StringBuilder sb);

    bool TryComplete(bool isAsync);
}
