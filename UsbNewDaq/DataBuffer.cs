using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace UsbNewDaq
{
    class DataBuffer
    {
        private BlockingCollection<byte[]> ThreadData = new BlockingCollection<byte[]>(500);
        public void SetData(byte[] receiveData)
        {
            ThreadData.Add(receiveData);
        }

        public void SetDataDone()
        {
            ThreadData.CompleteAdding();
        }

        public void GetData(out byte[] receiveData)
        {
            try
            {
                receiveData = ThreadData.Take();
            }
            catch (InvalidOperationException)
            {
                receiveData = null;
            }
        }
        public bool IsCompleted
        {
            get
            {
                return ThreadData.IsCompleted;
            }
        }
    }
}
