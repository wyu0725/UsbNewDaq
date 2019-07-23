using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Threading;
using CyUSB;
using System.Collections.Concurrent;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Threading;
using Microsoft.Win32;

namespace UsbNewDaq
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        USBDeviceList usbDevices;
        CyUSBDevice MyDevice;
        CyBulkEndPoint BulkInEndPoint;
        byte UsbInEndPointNum;
        CyBulkEndPoint BulkOutEndPoint;
        byte UsbOutEndPointNum;
        bool bRunning = false;
        private CancellationTokenSource DataAcqCancelToken;
        private CancellationTokenSource WriteFileCancelToken;
        private delegate void DisplayPacketInfo(int packageNum, int Failure, double Speed );
        private delegate void CheckPkgErrUpdate(int errNum);
        private delegate void CheckPkgErrUpdate1(int errNum);
        private delegate void CheckPkgErrUpdate2(int errNum);
        int XferSize = 512;
        string AbsoluteFileName = null;
        long Successes;
        long Failures;
        DateTime t1, t2;
        TimeSpan elapsed;
        double XferBytes;
        double xferRate;
        int BufSz;
        int QueueSz = 1;
        int PPX;
        bool IsUsbFX3 = false;
        bool IsLittleEndian = true;
        DataBuffer AcqDataBuffer = new DataBuffer();
        BlockingCollection<byte[]> ThreadData = new BlockingCollection<byte[]>();

        public MainWindow()
        {
            InitializeComponent();

            // Create the list of USB devices attached to the CyUSB3.sys driver.
            usbDevices = new USBDeviceList(CyConst.DEVICES_CYUSB);

            //Assign event handlers for device attachment and device removal.
            usbDevices.DeviceAttached += new EventHandler(usbDevices_DeviceAttached);
            usbDevices.DeviceRemoved += new EventHandler(usbDevices_DeviceRemoved);

            RefreshUsb();
            
        }

        private bool SaveFile(string filePath, string fileName)
        {
            string report;
            if (filePath.Trim() == null)
            {
                MessageBox.Show("Path not exist", "File ERROR", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
            if (!Directory.Exists(filePath))
            {
                Directory.CreateDirectory(filePath);
                report = string.Format("Dictory \"{0}\" created", filePath);
                tbxInfo.AppendText(report);
            }
            string DefaultFileName = DateTime.Now.ToString();
            string absoluteFileName;
            DefaultFileName = DefaultFileName.Replace("/", "_");
            DefaultFileName = DefaultFileName.Replace(":", "_");
            DefaultFileName = DefaultFileName.Replace(" ", "-");
            DefaultFileName += ".dat";
            if (fileName.Trim() == null || fileName == ".dat")
            {
                absoluteFileName = System.IO.Path.Combine(filePath, DefaultFileName);
                tbxFileName.Text = DefaultFileName;
            }
            else
            {
                absoluteFileName = System.IO.Path.Combine(filePath, fileName);
            }

            FileStream fileStream = null;
            if (File.Exists(absoluteFileName))
            {
                if (MessageBox.Show("File Exist. Use default name?", "File Error", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                {
                    absoluteFileName = System.IO.Path.Combine(filePath, DefaultFileName);
                    fileStream = File.Create(absoluteFileName);
                    tbxFileName.Text = DefaultFileName;
                }
                else
                {
                    return false;
                }
            }
            else
            {
                fileStream = File.Create(absoluteFileName);
            }
            fileStream.Close();
            AbsoluteFileName = absoluteFileName;
            report = string.Format("File: \"{0}\" Created", AbsoluteFileName);
            return true;
        }
        private bool SaveFile(string fileName)
        {
            return SaveFile(tbxReceiveDataPath.Text, fileName);
        }
        private bool SaveFile()
        {
            return SaveFile(tbxFileName.Text);
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            MessageBoxResult key = MessageBox.Show(
                "Are you sure you want to quit",
                "Confirm",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question,
                MessageBoxResult.No);
            e.Cancel = (key == MessageBoxResult.No);
        }

        void usbDevices_DeviceRemoved(object sender, EventArgs e)
        {
            bRunning = false;

            if (!ThreadData.IsAddingCompleted)
                ThreadData.CompleteAdding();

            MyDevice = null;
            BulkInEndPoint = null;
            BulkOutEndPoint = null;
            RefreshDeviceLists(false);
            RefreshEndPoint();
            tbxInfo.AppendText("USB Disconnect\n");
            lblUsbName.Content = "C# USB - no device";
            lblUsbName.Foreground = Brushes.Red;
            btnStartAcq.IsEnabled = false;
            btnCmdSend.IsEnabled = false;
            btnStartAcq.Content = "Start";
            btnSetUsb.Content = "Set";
            btnStartAcq.Background = new SolidColorBrush(Color.FromRgb(27, 129, 62));
            cbxDeviceLists.IsEnabled = true;
            btnSetUsb.IsEnabled = true;
            cbxInEndPoint.IsEnabled = true;
            cbxOutEndPoint.IsEnabled = true;
            cbxPpx.IsEnabled = true;
            cbxXferQueue.IsEnabled = true;
        }

        /*Summary
           This is the event handler for device attachment. This method  searches for the device with 
           VID-PID 04b4-00F1
        */
        void usbDevices_DeviceAttached(object sender, EventArgs e)
        {
            RefreshDeviceLists(false);
            RefreshEndPoint();
            cbxDeviceLists.IsEnabled = true;
            cbxInEndPoint.IsEnabled = true;
            cbxOutEndPoint.IsEnabled = true;
            cbxPpx.IsEnabled = true;
            cbxXferQueue.IsEnabled = true;
            rdbFx2.IsEnabled = true;
            rdbFx3.IsEnabled = true;
        }

        void RefreshUsb()
        {
            RefreshDeviceLists(false);
            RefreshEndPoint();
        }

        /*Summary
           Search the device with VID-PID 04b4-00F1 and if found, select the end point
        */
        private void RefreshDeviceLists(bool bPreserveSelectDevice)
        {
            int nCurSelection = 0;
            if (cbxDeviceLists.Items.Count > 0)
            {
                nCurSelection = cbxDeviceLists.SelectedIndex;
                cbxDeviceLists.Items.Clear();
            }
            int nDeviceList = usbDevices.Count;
            for (int nCount = 0; nCount < nDeviceList; nCount++)
            {
                USBDevice fxDevice = usbDevices[nCount];
                string strmsg;
                strmsg = "(0x" + fxDevice.VendorID.ToString("X4") + "-0x" + fxDevice.ProductID.ToString("X4") + ") " + fxDevice.FriendlyName;
                cbxDeviceLists.Items.Add(strmsg);
            }

            
                
        }

        private void RefreshEndPoint()
        {
            cbxInEndPoint.Items.Clear();
            cbxOutEndPoint.Items.Clear();
            USBDevice dev = null;
            if (cbxDeviceLists.Items.Count > 0 && cbxDeviceLists.SelectedIndex != -1)
                dev = usbDevices[cbxDeviceLists.SelectedIndex];

            if (dev != null)
            {
                MyDevice = (CyUSBDevice)dev;
                if (MyDevice.BulkInEndPt == null || MyDevice.BulkOutEndPt == null)
                {
                    MessageBox.Show("Not bulk device. Please program the device", "USB ERROR", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                UsbInEndPointNum = MyDevice.BulkInEndPt.Address;
                UsbOutEndPointNum = MyDevice.BulkOutEndPt.Address;
                string InEndPoint = "0x" + UsbInEndPointNum.ToString("X") + " Size: " + MyDevice.BulkInEndPt.MaxPktSize.ToString();
                string OutEndPoint = "0x" + UsbOutEndPointNum.ToString("X") + " Size: " + MyDevice.BulkOutEndPt.MaxPktSize.ToString();
                cbxInEndPoint.Items.Add(InEndPoint);
                cbxOutEndPoint.Items.Add(OutEndPoint);
                cbxPpx.Text = "128"; //Set default value to 512 Packets
                cbxXferQueue.Text = "1";
                if (cbxInEndPoint.Items.Count > 0 && cbxOutEndPoint.Items.Count > 0)
                {

                    btnSetUsb.IsEnabled = true;
                    cbxInEndPoint.SelectedIndex = 0;
                    cbxOutEndPoint.SelectedIndex = 0;
                }
                else
                    btnSetUsb.IsEnabled = false;
            }
        }

        private bool SetDevice()
        {
            MyDevice = usbDevices[cbxDeviceLists.SelectedIndex] as CyUSBDevice;
            if(MyDevice != null && MyDevice.BulkInEndPt != null && MyDevice.BulkOutEndPt != null)
            {
                BulkInEndPoint = MyDevice.EndPointOf(UsbInEndPointNum) as CyBulkEndPoint;
                BulkOutEndPoint = MyDevice.EndPointOf(UsbOutEndPointNum) as CyBulkEndPoint;
                lblUsbName.Content = MyDevice.FriendlyName + " Connected";
                lblUsbName.Foreground = Brushes.Green;
                IsUsbFX3 = rdbFx3.IsChecked == true;
                return true;
            }
            return false;
        }

        private void btnSetUsb_Click(object sender, RoutedEventArgs e)
        {
            if (btnSetUsb.Content.Equals("Set"))
            {
                if (cbxDeviceLists.SelectedIndex == -1)
                {
                    MessageBox.Show("Please select the USB device", "USB ERROR", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                if (SetDevice())
                {
                    cbxDeviceLists.IsEnabled = false;
                    cbxInEndPoint.IsEnabled = false;
                    cbxOutEndPoint.IsEnabled = false;
                    cbxPpx.IsEnabled = false;
                    cbxXferQueue.IsEnabled = false;
                    tbxInfo.AppendText("USB Connect\n");
                    btnSetUsb.Content = "Reset";
                    btnStartAcq.IsEnabled = true;
                    btnCmdSend.IsEnabled = true;
                    rdbFx2.IsEnabled = false;
                    rdbFx3.IsEnabled = false;
                }
                else
                {
                    MessageBox.Show("No Bulk EndPoint. Please program the Device","Device ERROR", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            else
            {
                MyDevice = null;
                BulkInEndPoint = null;
                BulkOutEndPoint = null;
                cbxDeviceLists.IsEnabled = true;
                cbxInEndPoint.IsEnabled = true;
                cbxOutEndPoint.IsEnabled = true;
                cbxPpx.IsEnabled = true;
                cbxXferQueue.IsEnabled = true;
                tbxInfo.AppendText("USB Disconnect\n");
                lblUsbName.Content = "C# USB - no device";
                lblUsbName.Foreground = Brushes.Red;
                btnStartAcq.IsEnabled = false;
                btnCmdSend.IsEnabled = false;
                btnSetUsb.Content = "Set";
                rdbFx2.IsEnabled = true;
                rdbFx3.IsEnabled = true;
            }
        }

        private void cbxPpx_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (BulkInEndPoint == null && BulkOutEndPoint == null)
                return;
            int ppx = Convert.ToUInt16(cbxPpx.Text);
            int len = BulkInEndPoint.MaxPktSize * ppx;
            int maxLen = 0x400000; // 4MBytes
            if (len > maxLen)
            {
                //ppx = maxLen / (EndPoint.MaxPktSize) / 8 * 8;
                if (BulkInEndPoint.MaxPktSize == 0)
                {
                    MessageBox.Show("Please correct MaxPacketSize in Descriptor", "Invalid MaxPacketSize");
                    return;
                }
                ppx = maxLen / (BulkInEndPoint.MaxPktSize);
                ppx -= (ppx % 8);
                MessageBox.Show("Maximum of 4MB per transfer.  Packets reduced.", "Invalid Packets per Xfer.");

            }
        }

        private void cbxDeviceLists_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            RefreshEndPoint();
        }

        private void TestGetDataSync()
        {
            byte[] acqData = new byte[512];
            while (bRunning)
            {
                XferSize = 512;
                if (BulkInEndPoint.XferData(ref acqData, ref XferSize, true))
                {
                    if(XferSize != 0)
                        ThreadData.Add(acqData);
                }
            }
            ThreadData.CompleteAdding();
        }

        private void TestDataProducer()
        {
            byte[] TestData = new byte[1024];
            byte[] ByteToWrite = new byte[1024];
            int byteCount = 0;
            for(int i = 0; bRunning; i++)
            {
                if(byteCount == 1024)
                {
                    byteCount = 0;
                    ByteToWrite = TestData;
                    ThreadData.Add(ByteToWrite);
                    Thread.Sleep(0);
                }
                TestData[byteCount] = (byte)i;
                TestData[byteCount + 1] = (byte)(i >> 8);
                TestData[byteCount + 2] = (byte)(i >> 16);
                TestData[byteCount + 3] = (byte)(i >> 24);
                byteCount += 4;
                
            }
            ThreadData.CompleteAdding();
        }

        private void btnStartAcq_Click(object sender, RoutedEventArgs e)
        {
            if (btnStartAcq.Content.Equals("Start"))
            {
                if (!SaveFile())
                {
                    return;
                }
                btnStartAcq.Content = "Stop";
                btnStartAcq.Background = new SolidColorBrush(Color.FromRgb(181, 93, 76));
                btnSetUsb.IsEnabled = false;
                //DataBuffer AcqDataBuffer = new DataBuffer();
                ThreadData = new BlockingCollection<byte[]>(556);
                // byte[] AcqData;
                //UsbDataProducer producer = new UsbDataProducer(BulkInEndPoint);
                BufSz = BulkInEndPoint.MaxPktSize * short.Parse(cbxPpx.Text);
                QueueSz = int.Parse(cbxXferQueue.Text);
                //QueueSz = 1;
                PPX = int.Parse(cbxPpx.Text);
                tbxCheckPkgErr.Text = string.Format("0");
                
                BinaryWriter bw = new BinaryWriter(File.Open(AbsoluteFileName, FileMode.Append));
                bRunning = true;
                var saveTask = Task.Factory.StartNew(() => 
                {
                    byte[] DataToFile;
                    int LastPackage = 0;
                    int FirstPackage = 1;
                    int PackageErrCnt = -1;
                    int PkgLength = 0;
                    CheckPkgErrUpdate dpCheckErr = new CheckPkgErrUpdate((x) => UpdateCheckPkgErr(x));
                    foreach (var item in ThreadData.GetConsumingEnumerable())
                    {
                        DataToFile = item;
                        FirstPackage = DataToFile[0] + (DataToFile[1] << 8) + (DataToFile[2] << 16) + (DataToFile[3] << 24);
                        PkgLength = DataToFile.Length;
                        bw.Write(DataToFile);
                        if (FirstPackage != LastPackage + 1)
                        {
                            PackageErrCnt += 1;
                            Dispatcher.Invoke(dpCheckErr, PackageErrCnt);
                        }
                        LastPackage = DataToFile[PkgLength - 4] + (DataToFile[PkgLength - 3] << 8) + (DataToFile[PkgLength - 2] << 16) + (DataToFile[PkgLength - 1] << 24);
                    }
                    bw.Flush();
                    bw.Dispose();
                    bw.Close();
                });
                Thread.Sleep(1000);
                bRunning = true;
                var producerTask = Task.Factory.StartNew(() => DataProducer());
                //var producerTask = Task.Run(() => TestDataProducer());
                //tListen = new Thread(new ThreadStart(DataProducer));
                //tListen = new Thread(new ThreadStart(TestDataProducer));
                //tListen.IsBackground = true;
                //tListen.Priority = ThreadPriority.Highest;
                //tListen.Start();
                /*var acqTask = Task.Run(() =>
                {
                    while (bRunning)
                    {
                        producer.DataAcq(XferSize, out AcqData);
                        AcqDataBuffer.SetData(AcqData);
                    }
                    AcqDataBuffer.SetDataDone();
                });*/

                //saveTask.Wait();
            }
            else
            {
                btnStartAcq.Content = "Start";
                btnStartAcq.Background = new SolidColorBrush(Color.FromRgb(27, 129, 62));
                bRunning = false;
                btnSetUsb.IsEnabled = true;
            }
        }

        private void DataProducer()
        {
            // Setup the queue buffers
            byte[][] cmdBufs = new byte[QueueSz][];
            byte[][] xferBufs = new byte[QueueSz][];
            byte[][] ovLaps = new byte[QueueSz][];
            ISO_PKT_INFO[][] pktsInfo = new ISO_PKT_INFO[QueueSz][];

            //int xStart = 0;

            //////////////////////////////////////////////////////////////////////////////
            ///////////////Pin the data buffer memory, so GC won't touch the memory///////
            //////////////////////////////////////////////////////////////////////////////

            GCHandle cmdBufferHandle = GCHandle.Alloc(cmdBufs[0], GCHandleType.Pinned);
            GCHandle xFerBufferHandle = GCHandle.Alloc(xferBufs[0], GCHandleType.Pinned);
            GCHandle overlapDataHandle = GCHandle.Alloc(ovLaps[0], GCHandleType.Pinned);
            GCHandle pktsInfoHandle = GCHandle.Alloc(pktsInfo[0], GCHandleType.Pinned);

            try
            {
                LockNLoad(cmdBufs, xferBufs, ovLaps, pktsInfo);
            }
            catch (NullReferenceException e)
            {
                // This exception gets thrown if the device is unplugged 
                // while we're streaming data
                e.GetBaseException();
                //this.Invoke(handleException);
            }

            //////////////////////////////////////////////////////////////////////////////
            ///////////////Release the pinned memory and make it available to GC./////////
            //////////////////////////////////////////////////////////////////////////////
            cmdBufferHandle.Free();
            xFerBufferHandle.Free();
            overlapDataHandle.Free();
            pktsInfoHandle.Free();
        }

        public unsafe void LockNLoad(byte[][] cBufs, byte[][] xBufs, byte[][] oLaps, ISO_PKT_INFO[][] pktsInfo)
        {
            int j = 0;
            int nLocalCount = j;

            GCHandle[] bufSingleTransfer = new GCHandle[QueueSz];
            GCHandle[] bufDataAllocation = new GCHandle[QueueSz];
            GCHandle[] bufPktsInfo = new GCHandle[QueueSz];
            GCHandle[] handleOverlap = new GCHandle[QueueSz];

            while (j < QueueSz)
            {
                // Allocate one set of buffers for the queue, Buffered IO method require user to allocate a buffer as a part of command buffer,
                // the BeginDataXfer does not allocated it. BeginDataXfer will copy the data from the main buffer to the allocated while initializing the commands.
                cBufs[j] = new byte[CyConst.SINGLE_XFER_LEN];

                xBufs[j] = new byte[BufSz];

                //initialize the buffer with initial value 0xA5
                for (int iIndex = 0; iIndex < BufSz; iIndex++)
                    xBufs[j][iIndex] = 0;

                int sz = Math.Max(CyConst.OverlapSignalAllocSize, sizeof(OVERLAPPED));
                oLaps[j] = new byte[sz];
                pktsInfo[j] = new ISO_PKT_INFO[PPX];

                /*/////////////////////////////////////////////////////////////////////////////
                 * 
                 * fixed keyword is getting thrown own by the compiler because the temporary variables 
                 * tL0, tc0 and tb0 aren't used. And for jagged C# array there is no way, we can use this 
                 * temporary variable.
                 * 
                 * Solution  for Variable Pinning:
                 * Its expected that application pin memory before passing the variable address to the
                 * library and subsequently to the windows driver.
                 * 
                 * Cypress Windows Driver is using this very same memory location for data reception or
                 * data delivery to the device.
                 * And, hence .Net Garbage collector isn't expected to move the memory location. And,
                 * Pinning the memory location is essential. And, not through FIXED keyword, because of 
                 * non-usability of temporary variable.
                 * 
                /////////////////////////////////////////////////////////////////////////////*/
                //fixed (byte* tL0 = oLaps[j], tc0 = cBufs[j], tb0 = xBufs[j])  // Pin the buffers in memory
                //////////////////////////////////////////////////////////////////////////////////////////////
                bufSingleTransfer[j] = GCHandle.Alloc(cBufs[j], GCHandleType.Pinned);
                bufDataAllocation[j] = GCHandle.Alloc(xBufs[j], GCHandleType.Pinned);
                bufPktsInfo[j] = GCHandle.Alloc(pktsInfo[j], GCHandleType.Pinned);
                handleOverlap[j] = GCHandle.Alloc(oLaps[j], GCHandleType.Pinned);
                // oLaps "fixed" keyword variable is in use. So, we are good.
                /////////////////////////////////////////////////////////////////////////////////////////////            

                unsafe
                {
                    //fixed (byte* tL0 = oLaps[j])
                    {
                        CyUSB.OVERLAPPED ovLapStatus = new CyUSB.OVERLAPPED();
                        ovLapStatus = (CyUSB.OVERLAPPED)Marshal.PtrToStructure(handleOverlap[j].AddrOfPinnedObject(), typeof(CyUSB.OVERLAPPED));
                        ovLapStatus.hEvent = (IntPtr)PInvoke.CreateEvent(0, 0, 0, 0);
                        Marshal.StructureToPtr(ovLapStatus, handleOverlap[j].AddrOfPinnedObject(), true);

                        // Pre-load the queue with a request
                        int len = BufSz;
                        if (BulkInEndPoint.BeginDataXfer(ref cBufs[j], ref xBufs[j], ref len, ref oLaps[j]) == false)
                            Failures++;
                    }
                    j++;
                }
            }

            XferData(cBufs, xBufs, oLaps, pktsInfo, handleOverlap);          // All loaded. Let's go!

            unsafe
            {
                for (nLocalCount = 0; nLocalCount < QueueSz; nLocalCount++)
                {
                    CyUSB.OVERLAPPED ovLapStatus = new CyUSB.OVERLAPPED();
                    ovLapStatus = (CyUSB.OVERLAPPED)Marshal.PtrToStructure(handleOverlap[nLocalCount].AddrOfPinnedObject(), typeof(CyUSB.OVERLAPPED));
                    PInvoke.CloseHandle(ovLapStatus.hEvent);

                    /*////////////////////////////////////////////////////////////////////////////////////////////
                     * 
                     * Release the pinned allocation handles.
                     * 
                    ////////////////////////////////////////////////////////////////////////////////////////////*/
                    bufSingleTransfer[nLocalCount].Free();
                    bufDataAllocation[nLocalCount].Free();
                    bufPktsInfo[nLocalCount].Free();
                    handleOverlap[nLocalCount].Free();

                    cBufs[nLocalCount] = null;
                    xBufs[nLocalCount] = null;
                    oLaps[nLocalCount] = null;
                }
            }
            GC.Collect();
        }
        public unsafe void XferData(byte[][] cBufs, byte[][] xBufs, byte[][] oLaps, ISO_PKT_INFO[][] pktsInfo, GCHandle[] handleOverlap)
        {
            int k = 0;
            int len = 0;

            Successes = 0;
            Failures = 0;

            XferBytes = 0;
            t1 = DateTime.Now;
            long nIteration = 0;
            CyUSB.OVERLAPPED ovData = new CyUSB.OVERLAPPED();
            DisplayPacketInfo dp = new DisplayPacketInfo((int x, int y, double z) => UpdatePackageInfo(x, y, z));
            for (; bRunning;)
            {
                nIteration++;
                // WaitForXfer
                unsafe
                {
                    //fixed (byte* tmpOvlap = oLaps[k])
                    {
                        ovData = (CyUSB.OVERLAPPED)Marshal.PtrToStructure(handleOverlap[k].AddrOfPinnedObject(), typeof(CyUSB.OVERLAPPED));
                        if (!BulkInEndPoint.WaitForXfer(ovData.hEvent, 500))
                        {
                            BulkInEndPoint.Abort();
                            PInvoke.WaitForSingleObject(ovData.hEvent, 500);
                        }
                    }
                }

                // FinishDataXfer
                int FirstPackage;
                int LastPackage = 0;
                int PkgLength;
                if (BulkInEndPoint.FinishDataXfer(ref cBufs[k], ref xBufs[k], ref len, ref oLaps[k]))
                {
                    XferBytes += len;
                    //if(xBufs[k] != null)
                    if(len != 0)
                    {
                        byte[] DataToFile = new byte[len];
                        Array.Copy(xBufs[k], DataToFile, len);
                        //AcqDataBuffer.SetData(DataToFile);
                        ThreadData.Add(DataToFile);
                        FirstPackage = DataToFile[0] + (DataToFile[1] << 8) + (DataToFile[2] << 16) + (DataToFile[3] << 24);
                        PkgLength = DataToFile.Length;
                        LastPackage = DataToFile[PkgLength - 4] + (DataToFile[PkgLength - 3] << 8) + (DataToFile[PkgLength - 2] << 16) + (DataToFile[PkgLength - 1] << 24);
                    }
                    Successes++;
                }
                else
                    Failures++;


                // Re-submit this buffer into the queue
                len = BufSz;
                if (BulkInEndPoint.BeginDataXfer(ref cBufs[k], ref xBufs[k], ref len, ref oLaps[k]) == false)
                    Failures++;

                Thread.Sleep(10);

                k++;
                if (k == QueueSz)  // Only update displayed stats once each time through the queue
                {
                    k = 0;

                    t2 = DateTime.Now;
                    elapsed = t2 - t1;

                    xferRate = XferBytes / elapsed.TotalMilliseconds;
                    xferRate = xferRate / (1000.0);
                    Dispatcher.Invoke(dp, (int)Successes, (int)Failures, (double)xferRate);
                    // Call StatusUpdate() in the main thread
                    //if (bRunning == true) this.Invoke(updateUI);

                    // For small QueueSz or PPX, the loop is too tight for UI thread to ever get service.   
                    // Without this, app hangs in those scenarios.
                    Thread.Sleep(0);
                }
                Thread.Sleep(0);

            } // End infinite loop
            // Let's recall all the queued buffer and abort the end point.
            BulkInEndPoint.Abort();
            //AcqDataBuffer.SetDataDone();
            ThreadData.CompleteAdding();
        }
        private void UpdatePackageInfo(int packageNum, int packageFailure, double Speed)
        {
            tbxPackageCount.Text = packageNum.ToString();
            tbxFailurePkg.Text = packageFailure.ToString();
            tbxSpeed.Text = Speed.ToString("#0.00");
        }

        private void rdbCmdSourceFile_Checked(object sender, RoutedEventArgs e)
        {
            tbxCmdFileName.IsEnabled = true;
            btnSelectFile.IsEnabled = true;
            tbxCmdIn.IsEnabled = false;
            tbxCmdFileName.Text = Directory.GetCurrentDirectory();
        }

        private void rdbCmdSourceInput_Checked(object sender, RoutedEventArgs e)
        {
            if (tbxCmdFileName != null)
            {
                tbxCmdFileName.IsEnabled = false;
                btnSelectFile.IsEnabled = false;
                tbxCmdIn.IsEnabled = true;
            }
        }

        private void btnSelectFile_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openCmdFile = new OpenFileDialog();
            openCmdFile.Filter = "Data files (*.dat)|*.dat|Text files (.txt)|*.txt|All files (*.*)|*.*";
            openCmdFile.InitialDirectory = tbxCmdFileName.Text;
            if (openCmdFile.ShowDialog() == true)
                tbxCmdFileName.Text = openCmdFile.FileName;

        }

        private void btnCmdSend_Click(object sender, RoutedEventArgs e)
        {
            byte[] RawCmdBytes;
            if (rdbCmdSourceFile.IsChecked == true)
            {
                string CmdFileName = tbxCmdFileName.Text;
                if (CmdFileName == null || !File.Exists(CmdFileName))
                {
                    MessageBox.Show("File not found", "File ERROR", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                StringBuilder TotalCmdStringBuilder = new StringBuilder();
                using (StreamReader cmdFileStream = new StreamReader(CmdFileName))
                {
                    string[] hexMarks = new string[] { "0x", "0X"};
                    string CmdString = cmdFileStream.ReadLine();
                    while (CmdString != null)
                    {
                        CmdString = CmdString.Replace(" ",string.Empty);
                        if(CmdString.Contains("//"))
                        {
                            CmdString = cmdFileStream.ReadLine();
                            continue;
                        }
                        if ( CmdString.Contains("0X") || CmdString.Contains("0x"))
                        {
                            string[] cmdSubString = CmdString.Split(hexMarks, StringSplitOptions.RemoveEmptyEntries);
                            CmdString = string.Join("", cmdSubString);
                        }
                        if (CheckStringFormat.CheckHex(CmdString))
                        {
                            TotalCmdStringBuilder.Append(CmdString);
                        }
                        CmdString = cmdFileStream.ReadLine();
                    }
                    string TotalCmdString = TotalCmdStringBuilder.ToString();
                    RawCmdBytes = HexStringToByteArray(TotalCmdStringBuilder.ToString());
                }
            }
            else if(rdbCmdSourceInput.IsChecked == true)
            {
                string CmdIn = tbxCmdIn.Text.Replace(" ", string.Empty);
                string[] hexMarks = new string[] { "0x", "0X" };
                if (CmdIn.Contains("0X") || CmdIn.Contains("0x"))
                {
                    string[] cmdSubString = CmdIn.Split(hexMarks, StringSplitOptions.RemoveEmptyEntries);
                    CmdIn = string.Join("", cmdSubString);
                }
                if (CmdIn == "" || !CheckStringFormat.CheckHex(CmdIn))
                {
                    MessageBox.Show("Please input the cmd in HEX", "Command ERROR", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                RawCmdBytes = HexStringToByteArray(CmdIn);
            }
            else
            {
                return;
            }
            CommandSend(RawCmdBytes, BulkOutEndPoint, IsUsbFX3, IsLittleEndian);
        }

        private void btnClearInfo_Click(object sender, RoutedEventArgs e)
        {
            tbxInfo.Text = "";
        }

        private void UpdateCheckPkgErr(int errNum)
        {
            tbxCheckPkgErr.Text = errNum.ToString();
        }

        private byte[] HexStringToByteArray(string HexString)
        {
            int StringNum = HexString.Length;
            byte[] hexBytes = new byte[StringNum / 2];
            for (int i = 0; i < StringNum; i += 2)
            {
                hexBytes[i / 2] = Convert.ToByte(HexString.Substring(i, 2), 16);
            }
            return hexBytes;
        }

        private bool CommandSend(byte[] CmdBytes, CyBulkEndPoint usbBulkOutEndPoint)
        {
            int CmdLength = CmdBytes.Length;
            if (CmdLength == 0)
                return false;
            return usbBulkOutEndPoint.XferData(ref CmdBytes, ref CmdLength);
        }
        private bool CommandSend(byte[] CmdBytes, CyBulkEndPoint usbBulkOutEndPoint, bool IsSuperSpeed, bool IsLittleEndian)
        {
            byte[] CmdBytesToUsb = CommandLengthCheck(CmdBytes, IsSuperSpeed);
            if (IsLittleEndian)
            {
                CmdBytesToUsb = CommandEndianChange(CmdBytesToUsb, IsSuperSpeed);
            }
            return CommandSend(CmdBytesToUsb, usbBulkOutEndPoint);
        }

        private byte[] CommandEndianChange(byte[] CmdBytes, bool IsSuperSpeed)
        {
            int CmdLength = CmdBytes.Length;
            byte cmdTemp;
            if (IsSuperSpeed)
            {
                for(int i = 0; i < CmdLength; i += 4)
                {
                    cmdTemp = CmdBytes[i];
                    CmdBytes[i] = CmdBytes[i + 3];
                    CmdBytes[i + 3] = cmdTemp;
                    cmdTemp = CmdBytes[i + 1];
                    CmdBytes[i + 1] = CmdBytes[i + 2];
                    CmdBytes[i + 2] = cmdTemp;
                }
            }
            else
            {
                for(int i = 0; i < CmdLength; i += 2)
                {
                    cmdTemp = CmdBytes[i];
                    CmdBytes[i] = CmdBytes[i + 1];
                    CmdBytes[i + 1] = cmdTemp;
                }
            }
            return CmdBytes;
        }

        private void rdbLittleEndian_Checked(object sender, RoutedEventArgs e)
        {
            IsLittleEndian = true;
        }

        private void rdbBigEndian_Checked(object sender, RoutedEventArgs e)
        {
            IsLittleEndian = false;
        }

        private void ConfigFX3Device(string FileName, CyUSBDevice myDevice)
        {
            if (MyDevice != null)
            {
                FX3_FWDWNLOAD_ERROR_CODE enmResult = FX3_FWDWNLOAD_ERROR_CODE.SUCCESS;
                CyFX3Device fx3 = myDevice as CyFX3Device;
                enmResult = fx3.DownloadFw(FileName, FX3_FWDWNLOAD_MEDIA_TYPE.RAM);
            }
            else
            {
                MessageBox.Show("Please select the USB device", "Device ERROR", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ConfigFX2Device(string FileName, CyUSBDevice myDevice)
        {
            if (MyDevice != null)
            {
                CyFX2Device fx2 = myDevice as CyFX2Device;
                bool bResult = fx2.LoadRAM(FileName);
                if (bResult)
                {
                    tbxInfo.AppendText("Config FX2 Device successfully\n");
                }
            }
            else
            {
                MessageBox.Show("Please select the USB device", "Device ERROR", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }


        private void mitResetDevice_Click(object sender, RoutedEventArgs e)
        {
            if (MyDevice != null)
            {
                MyDevice.Reset();
                mitResetDevice.IsEnabled = false;
                mitConfigDevice.IsEnabled = true;
            }
            else
            {
                MessageBox.Show("Please select the USB device", "Device ERROR", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void mitConfigDevice_Click(object sender, RoutedEventArgs e)
        {
            string ConfigFileName;
            OpenFileDialog openCmdFile = new OpenFileDialog();
            openCmdFile.Filter = "FX3 IMG (*.img)|*.img|FX2 iic (.iic)|*.iic|All files (*.*)|*.*";
            openCmdFile.InitialDirectory = Directory.GetCurrentDirectory();

            if (openCmdFile.ShowDialog() == true)
                ConfigFileName = openCmdFile.FileName;
            else
            {
                return;
            }

            if (ConfigFileName == null)
            {
                return;
            }

            if (rdbFx3.IsChecked == true)
            {
                ConfigFX3Device(ConfigFileName, MyDevice);
            }
            else if (rdbFx2.IsChecked == true)
            {
                ConfigFX2Device(ConfigFileName, MyDevice);
            }
            mitResetDevice.IsEnabled = true;
            mitConfigDevice.IsEnabled = false;
        }


        private void btnSaveFile_Click(object sender, RoutedEventArgs e)
        {
            SaveFileDialog saveDialog = new SaveFileDialog();
            saveDialog.DefaultExt = "dat";
            saveDialog.AddExtension = true;
            saveDialog.Filter = "data file (*.dat)|*.dat|text file (.txt)|*.txt|All files (*.*)|*.*";
            saveDialog.FileName = tbxFileName.Text;
            saveDialog.InitialDirectory = @tbxReceiveDataPath.Text;
            saveDialog.OverwritePrompt = true;
            saveDialog.Title = "Save Data files";
            saveDialog.ValidateNames = true;
            if (saveDialog.ShowDialog() == true)
            {
                tbxFileName.Text = saveDialog.SafeFileName;
                string FileName = saveDialog.FileName;
                string[] fileSplit = new string[1] { "\\" };
                string[] FileNameArray = FileName.Split(fileSplit, StringSplitOptions.None);
                FileName = null;
                string[] FilePathArray = new string[FileNameArray.Length - 1];
                for (int i = 0; i<FileNameArray.Length - 1; i++)
                {
                    FilePathArray[i] = FileNameArray[i];
                }
                tbxReceiveDataPath.Text = string.Join("\\", FilePathArray);
            }

        }

        private byte[] CommandLengthCheck(byte[] CmdBytes, bool IsSuperSpeed)
        {
            int CmdLength = CmdBytes.Length;
            int CmdNum;
            int CmdNumRes;
            int NewCmdLength;
            int CmdLengthDivider;
            if (IsSuperSpeed)
            {
                CmdLengthDivider = 4;
            }
            else
            {
                CmdLengthDivider = 2;
            }
            CmdNum = CmdLength / CmdLengthDivider;
            CmdNumRes = CmdLength % CmdLengthDivider;
            if (CmdNumRes != 0)
            {
                NewCmdLength = (CmdNum + 1) * CmdLengthDivider;
            }
            else
            {
                NewCmdLength = CmdNum * CmdLengthDivider;
            }
            byte[] NewCmdBytes = new byte[NewCmdLength];
            for (int i = 0; i < CmdNum*CmdLengthDivider; i++)
            {
                NewCmdBytes[i] = CmdBytes[i];
            }
            for (int i = 0; i < CmdNumRes; i++)
            {
                NewCmdBytes[NewCmdLength - i - 1] = CmdBytes[CmdLength - i - 1];
            }
            
            return NewCmdBytes;
        }
        
    }


}
