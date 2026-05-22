using System;
using System.Collections.Generic;
using System.Text;

namespace DynamicDtoCore.Tester
{
    //this interface is used to test the generic version of the Select method, which will create a dynamic class that implements this interface
    public interface IProduct
    {
        string Name { get; set; }
        DateTime ModifiedDate { get; set; }
    }
}
