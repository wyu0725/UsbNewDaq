using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UsbNewDaq
{
    interface IDataProducer
    {
        void DataAcq(int XferSizw, out byte[] acqData);
    }
}
