using System;
using System.Collections.Generic;
using System.Text;

namespace DynamicDtoCore.Tester
{
    public interface IProduct
    {
        string Name { get; set; }
        DateTime ModifiedDate { get; set; }
    }
}
