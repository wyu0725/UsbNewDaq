using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using CyUSB;

namespace UsbNewDaq
{
    class UsbDataProducer: IDataProducer
    {
        public CyBulkEndPoint BulkInEndPoint
        {
            get
            {
                return _bulkInEndPoint; 
            }
        }
        private CyBulkEndPoint _bulkInEndPoint;

        public UsbDataProducer(CyBulkEndPoint usbEndPoint)
        {
            if (usbEndPoint.bIn)
                _bulkInEndPoint = usbEndPoint;
            else
                _bulkInEndPoint = null;
        }

        public void DataAcq(int XferSize, out byte[] acqData)
        {
            acqData = new byte[XferSize];
            if(BulkInEndPoint == null)
            {
                acqData = null;
                return;
            }
            BulkInEndPoint.XferData(ref acqData, ref XferSize, true);

        }

        public void DataAcq(int QueueSz)
        {
            
        }

        

        

    }
}
