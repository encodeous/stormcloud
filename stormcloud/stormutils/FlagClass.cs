using System;
using System.Collections.Generic;
using System.Text;

namespace stormutils
{
    public class FlagClass<T>
    {
        public T flag;
        public FlagClass(T obj)
        {
            flag = obj;
        }
    }
}
