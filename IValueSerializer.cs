using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace AngiesList.Redis
{
    public interface IValueSerializer
    {
        byte[] Serialize(object value);
        object Deserialize(byte[] bytes);
    }
}
