using System.Collections.Generic;

namespace CodeBrix.Redis; //was previously: StackExchange.Redis;

internal interface IMultiMessage
{
    IEnumerable<Message> GetMessages(PhysicalConnection connection);
}
