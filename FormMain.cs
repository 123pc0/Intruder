using Emgu.CV;
using Emgu.CV.CvEnum;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Threading;
using System.Windows.Forms;
using Emgu.CV.Cvb;
using Emgu.CV.Structure;
using Emgu.CV.Cuda;
using System.Collections;
using System.Diagnostics;
using System.Timers;
using Timer = System.Timers.Timer;
using System.Runtime.InteropServices;

namespace Intruder_rev01
{
    public partial class FormMain : Form
	{
        private const int CHANNELNUMBER = 10;
        private const int TIMER_LOGGING = 10000;//MS




        //logfiles
        private static Timer logTimer;


        //private static ArrayList[] log = new ArrayList[CHANNELNUMBER];
        //int logLine = 0;
        Size pictureboxWH;


        private static void minimizeMemory()
        {
            GC.Collect(GC.MaxGeneration);
            GC.WaitForPendingFinalizers();
            SetProcessWorkingSetSize(Process.GetCurrentProcess().Handle,
                (UIntPtr)0xFFFFFFFF, (UIntPtr)0xFFFFFFFF);
        }

        [DllImport("kernel32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetProcessWorkingSetSize(IntPtr process,
            UIntPtr minimumWorkingSetSize, UIntPtr maximumWorkingSetSize);



        public FormMain()
		{
            minimizeMemory();
            InitializeComponent();

            CvInvoke.UseOpenCL = false;
            InitialButtonCamera();
            //Check file config
            //first init config
            InitialChannelConfiguration();
            //
            InitialDatabaseCamera();
            

            NewPolygon = null;
            pictureboxWH = new Size(pictureBox1.Width, pictureBox1.Height);

            //First Page
            textBox_cameraProperties_channelNumber.Text = "Channel-Number";
            textBox_cameraProperties_channelNumber.Enabled = false;
            textBox_cameraProperties_locationName.Enabled = false;
            textBox_cameraProperties_ipAddress.Enabled = false;
            textBox_cameraProperties_ipAddress.Text = "0.0.0.0";
            textBox_cameraProperties_locationName.Text = "Place";


            trackBar_minimum.Value = 1;
            trackBar_maximum.Value = 5000;
            textBoxMinFilter.Text = trackBar_minimum.Value.ToString();
            textBoxMaxFilter.Text = trackBar_maximum.Value.ToString();


            textBox_cameraProperties_loggingPath.Text = (Directory.GetCurrentDirectory() + "\\log_" + choseCamera.ToString() + ".txt").ToString();

            button_cameraControl_checkConnection.Enabled = true;
            button_loadCameraImage.Enabled = false;
            button_enableMotion.Enabled = false;
            button_disableMotion.Enabled = false;

            //Timer for logging
            SetTimer();

        }

        private void SetTimer()
        {
            logTimer = new Timer();
            logTimer.Elapsed += new ElapsedEventHandler(OnTimeEvent);
            logTimer.Interval = TIMER_LOGGING;
            logTimer.Start();
        }

        private void OnTimeEvent(object sender, EventArgs e)
        {
            Console.WriteLine(DateTime.Now);
            try
            {
                for (int i = 0; i < CHANNELNUMBER; i++)
                {
                    if (signalAlarm[i] == true)
                    {
                        string result = DateTime.Now + "\t| " + site_all_ip[i] + "\r\n";
                        File.AppendAllText("log_" + (i+1).ToString() + ".txt", result);
                        signalAlarm[i] = false;
                    }
                }
            }
            catch (NullReferenceException excpt)
            {
                MessageBox.Show(excpt.Message);
            }
        }

        #region InitialButtonCamera
        const byte countButton = 40;
        private void InitialButtonCamera()
        {// button in panel
            for (int i = 1; i <= countButton; i++)
            {
                string buttonName = "button_channel" + i;
                tableLayoutPanel1.Controls[buttonName].Enabled = false;
                tableLayoutPanel1.Controls[buttonName].Click += new EventHandler(this.button_camera_choose_Click);
            }
            for (int i = 0; i < CHANNELNUMBER; i++)
            {
                int localNum = i + 1;
                string buttonName = "button_channel" + localNum;

                tableLayoutPanel1.Controls[buttonName].Enabled = true;
            }
        }
        #endregion

        #region InitialCahnelConfiguration
        private void InitialChannelConfiguration()
        {
            CheckChannelConfiguration();
        }
        private void CheckChannelConfiguration()
        {
            // Check config folder exist
            if (Directory.Exists(Directory.GetCurrentDirectory() + "\\config"))
            {
                Console.WriteLine("Aleady have configuration");
                return;
            }
            Console.WriteLine("Create new default configuration.");

            // Create channel configuration folder
            System.IO.Directory.CreateDirectory(Directory.GetCurrentDirectory() + "\\config");

            // Create channel configuration files
            for (int i = 0; i < CHANNELNUMBER; i++)
            {
                CreateChannelConfiguration(Directory.GetCurrentDirectory() + "\\config", "channel" + (i + 1).ToString("00"));
            }

        }
        private void CreateChannelConfiguration(string path, string name)
        {
            // Config file path
            string filePath = path + '\\' + name + ".conf";

            // Check exist file
            if (File.Exists(filePath))
            {
                return;
            }
            else
            {
                // Create config file
                TextWriter tw = new StreamWriter(filePath);

                // Write CHANNEL NUMBER
                tw.WriteLine(name);
                // Write LOCATION NAME
                tw.WriteLine("default location " + name);
                // Write IP ADDRESS
                tw.WriteLine("0.0.0.0");

                // Write area detection x
                tw.WriteLine("1|200|200|1");

                // Write area detection y
                tw.WriteLine("1|1|200|200");

                // Write DETECTION SENSIVITY 
                // [0,1,2] Defualt:1
                // 0:LOW, 1:MEDIUM, 2:HIGH ////////////////////////////////////////////////////////////////////////////////// Waiting for edit
                tw.WriteLine("1"); //low 
                tw.WriteLine("65535"); //high
                // Close config file
                tw.Close();
            }
        }

        #endregion



        #region InitialDatabaseCamera

        private string[] cameraLocation;
        private string[] cameraIPAddress;
        private string[] cameraPoint_x;// { get; set; }
        private string[] cameraPoint_y;// { get; set; }
        private string[] cameraSensitivityLow;// { get; set; }
        private string[] cameraSensitivityHigh;// { get; set; }

        private string[] LoadChannelConfiguration(string path)
        {
            // Check exist file
            if (File.Exists(path))
            {
                return System.IO.File.ReadAllLines(path);
            }
            else
            {
                return null;
            }
        }

        private void InitialDatabaseCamera()
		{
			// Create parameters
			this.cameraLocation = new string[CHANNELNUMBER];
			this.cameraIPAddress = new string[CHANNELNUMBER];
            this.cameraPoint_x = new string[CHANNELNUMBER];
            this.cameraPoint_y = new string[CHANNELNUMBER];
            this.cameraSensitivityLow = new string[CHANNELNUMBER];
            this.cameraSensitivityHigh = new string[CHANNELNUMBER];

            // Load channel parameters
            for (int i=0; i< CHANNELNUMBER; i++)
			{
				string[] cameraParametersLine = LoadChannelConfiguration(Directory.GetCurrentDirectory() + "\\config\\channel" + (i + 1).ToString("00") + ".conf");
				if (cameraParametersLine != null)
				{
					this.cameraLocation[i] = cameraParametersLine[1];
                    Console.WriteLine(cameraLocation[i].ToString());

					this.cameraIPAddress[i] = cameraParametersLine[2];
                    Console.WriteLine(cameraIPAddress[i].ToString());

                    this.cameraPoint_x[i] = cameraParametersLine[3];
                    Console.WriteLine(cameraPoint_x[i].ToString());

                    this.cameraPoint_y[i] = cameraParametersLine[4];
                    Console.WriteLine(cameraPoint_y[i].ToString());

                    this.cameraSensitivityLow[i] = cameraParametersLine[5];
                    Console.WriteLine(cameraSensitivityLow[i].ToString());

                    this.cameraSensitivityHigh[i] = cameraParametersLine[6];
                    Console.WriteLine(cameraSensitivityHigh[i].ToString());
                }
			}
		}
        #endregion
        // Comment for p'pao
       

		private bool CheckAddress(string ipaddress)
		{
			if (ipaddress != "0.0.0.0")

			{
				Ping pinger = new Ping();
				PingReply reply = pinger.Send(ipaddress);
				DateTime t = DateTime.Now;
				if (reply.Status == IPStatus.Success)
				{
					Console.WriteLine("Ping: " + ipaddress + " success");
					return true;
				}
				else
				{
					Console.WriteLine("Ping: " + ipaddress + " failed");
					return false;
				}
			}
			else
				return false;
		}
        /// Start Process following manual //////////////////////////////////////////////////////////////////////////////////////////////////////////////

        #region  STEP 2: CHECK CAMERA CONNECTION [finish]
        private void button_camera_checkConnection_Click(object sender, EventArgs e)
        {
            //DateTime t = DateTime.Now;
            //Parallel.ForEach(cameraIPAddress, item => CheckAddress(item));
            //Console.WriteLine("Ping time " + "(" + (DateTime.Now - t).ToString() + ")");

            button_cameraControl_checkConnection.Enabled = false;

            try
            {
                backgroundWorker_STEP2.RunWorkerAsync();
            }

            catch (NullReferenceException excpt)
            {
                MessageBox.Show(excpt.Message);
            }
        }

        #region pingCheck()
        static string[] site_all_ip = new string[]
        {
            "172.29.61.101",
            "172.29.61.102",
            "172.29.61.103",
            "172.29.61.104",
            "172.29.61.105",
            "172.29.61.106",
            "172.29.61.107",
            "172.29.61.108",
            "172.29.61.109",
            "172.29.61.110",
            "172.29.61.111",
            "172.29.61.112",
            "172.29.61.113",
            "172.29.61.114",
            "172.29.61.115",
            "172.29.61.116",
            "172.29.211.23",
            "172.29.211.25",
            "172.29.211.27",
            "172.29.211.29",
            "172.29.211.31",
            "172.29.211.33",
            "172.29.211.35",
            "172.29.211.37",
            "172.29.211.39",
            "172.29.211.21",
            "172.29.211.41",
            "172.29.210.51",
            "172.29.210.52",
            "172.29.210.53",
            "172.29.210.54",
            "172.29.210.55",
            "172.29.210.56",
            "172.29.210.57",
            "172.29.210.58",
            "172.29.210.59",
            "172.29.210.60"
        };
        static Thread[] threads = new Thread[CHANNELNUMBER];
        static Semaphore sem = new Semaphore(CHANNELNUMBER, CHANNELNUMBER);
        static Ping[] ping = new Ping[CHANNELNUMBER];
        static PingReply[] pingReply = new PingReply[CHANNELNUMBER];
        static bool[] isConnect = new bool[CHANNELNUMBER];
        static void pingStatus(int camID)
        {
            try
            {
                if (camID >= CHANNELNUMBER)
                    return;
                ping[camID] = new Ping();

                Console.WriteLine("{0} is waiting in line...", Thread.CurrentThread.Name);
                sem.WaitOne();

                pingReply[camID] = ping[camID].Send(site_all_ip[camID]);



                //// Check AVG ping
                //Console.WriteLine(pingReply[camID].RoundtripTime);

                //ping[camID].SendAsync(site_all_ip[camID], pingReply[camID]);
                if (pingReply[camID].Status.ToString().Equals("Success"))
                {
                    isConnect[camID] = true;
                    Console.WriteLine("success time: " + Thread.CurrentThread.Name);
                }
                else
                {
                    isConnect[camID] = false;
                    Console.WriteLine("failed time: " + Thread.CurrentThread.Name);
                }
                sem.Release();
            }

            catch (NullReferenceException excpt)
            {
                MessageBox.Show(excpt.Message);
            }
        }
        static void pingCheck()
        {
            try
            {
                for (int i = 0; i < CHANNELNUMBER; i++)
                {
                    int localNum = i;
                    threads[localNum] = new Thread(() => pingStatus(localNum));
                    threads[localNum].Name = "thread_" + localNum;
                    threads[localNum].Start();
                }
            }

            catch (NullReferenceException excpt)
            {
                MessageBox.Show(excpt.Message);
            }
        }
        #endregion
        #region highlightButton_ping()
        private void highlightButton_ping()
        {
            try
            {
                for (int i = 0; i < CHANNELNUMBER; i++)
                {
                    int localNum = i + 1;
                    string buttonName = "button_channel" + localNum;
                    //https://stackoverflow.com/questions/20773212/include-for-loop-in-button-name

                    if (isConnect[i])
                    {
                        tableLayoutPanel1.Controls[buttonName].Enabled = true;
                        tableLayoutPanel1.Controls[buttonName].BackColor = Color.FromArgb(255, 255, 128); //yellow                  
                    }
                    else
                    {
                        tableLayoutPanel1.Controls[buttonName].Enabled = false;
                        // tableLayoutPanel1.Controls[buttonName].BackColor = Color.FromArgb(128, 255, 128); //green
                    }
                }
            }

            catch (NullReferenceException excpt)
            {
                MessageBox.Show(excpt.Message);
            }
        }
        #endregion

        //STEP 2.1 Click “CHECK CONNECTION”
        private void backgroundWorker_STEP2_DoWork(object sender, System.ComponentModel.DoWorkEventArgs e)
        {
            try
            {
                pingCheck();
                int ix;
                for (ix = 0; ix < 6; ix++)
                {
                    Thread.Sleep(1000);
                    backgroundWorker_STEP2.ReportProgress(ix+1);
                }
            }

            catch (NullReferenceException excpt)
            {
                MessageBox.Show(excpt.Message);
            }
        }
        //STEP 2.2 Waiting Progress bar, the operation will complete in about five seconds or less.
        private void backgroundWorker_STEP2_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            try
            {
                int ix = e.ProgressPercentage;
                toolStripProgressBar1.Value = (ix * 100) / 6;
                toolStripStatusLabel_animate.Text = "Waiting... " + toolStripProgressBar1.Value + " %";
            }

            catch (NullReferenceException excpt)
            {
                MessageBox.Show(excpt.Message);
            }
}
        //STEP 2.3 After finishing process, button changed back-color based on signal.
        private void backgroundWorker_STEP2_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            try
            {
                Thread.Sleep(1000);
                toolStripStatusLabel_animate.Text = "Finish";
                highlightButton_ping();

                button_cameraControl_checkConnection.Enabled = false;
                button_loadCameraImage.Enabled = true;
                button_enableMotion.Enabled = false;
                button_enableMotion.Enabled = false;
            }

            catch (NullReferenceException excpt)
            {
                MessageBox.Show(excpt.Message);
            }
        }
        #endregion

        #region  STEP 3: LOAD CAMERA DATA [finish]
        private void button_loadCameraImage_Click(object sender, EventArgs e)
        {
            //load all camera data
            try
            {
                backgroundWorker_STEP3.RunWorkerAsync();
            }
            catch (NullReferenceException excpt)
            {
                MessageBox.Show(excpt.Message);
            }

        }

        #region Camera Run
        static string[] site_all = new string[]
        {
            "http://root:admin@172.29.61.101/axis-cgi/mjpg/video.cgi",
            "http://root:admin@172.29.61.102/axis-cgi/mjpg/video.cgi",
            "http://root:admin@172.29.61.103/axis-cgi/mjpg/video.cgi",
            "http://root:admin@172.29.61.104/axis-cgi/mjpg/video.cgi",
            "http://root:admin@172.29.61.105/axis-cgi/mjpg/video.cgi",
            "http://root:admin@172.29.61.106/axis-cgi/mjpg/video.cgi",
            "http://root:admin@172.29.61.107/axis-cgi/mjpg/video.cgi",
            "http://root:admin@172.29.61.108/axis-cgi/mjpg/video.cgi",
            "http://root:admin@172.29.61.109/axis-cgi/mjpg/video.cgi",
            "http://root:admin@172.29.61.110/axis-cgi/mjpg/video.cgi",
            "http://root:admin@172.29.61.111/axis-cgi/mjpg/video.cgi",
            "http://root:admin@172.29.61.112/axis-cgi/mjpg/video.cgi",
            "http://root:admin@172.29.61.113/axis-cgi/mjpg/video.cgi",
            "http://root:admin@172.29.61.114/axis-cgi/mjpg/video.cgi",
            "http://root:admin@172.29.61.115/axis-cgi/mjpg/video.cgi",
            "http://root:admin@172.29.61.116/axis-cgi/mjpg/video.cgi",
            "http://root:root@172.29.211.23/axis-cgi/mjpg/video.cgi",
            "http://root:root@172.29.211.25/axis-cgi/mjpg/video.cgi",
            "http://root:root@172.29.211.27/axis-cgi/mjpg/video.cgi",
            "http://root:root@172.29.211.29/axis-cgi/mjpg/video.cgi",
            "http://root:root@172.29.211.31/axis-cgi/mjpg/video.cgi",
            "http://root:root@172.29.211.33/axis-cgi/mjpg/video.cgi",
            "http://root:root@172.29.211.35/axis-cgi/mjpg/video.cgi",
            "http://root:root@172.29.211.37/axis-cgi/mjpg/video.cgi",
            "http://root:root@172.29.211.39/axis-cgi/mjpg/video.cgi",
            "http://root:root@172.29.211.21/axis-cgi/mjpg/video.cgi",
            "http://root:root@172.29.211.41/axis-cgi/mjpg/video.cgi",
            "http://root:Sintec1234@172.29.210.51/axis-cgi/mjpg/video.cgi",
            "http://root:Sintec1234@172.29.210.52/axis-cgi/mjpg/video.cgi",
            "http://root:Sintec1234@172.29.210.53/axis-cgi/mjpg/video.cgi",
            "http://root:Sintec1234@172.29.210.54/axis-cgi/mjpg/video.cgi",
            "http://root:Sintec1234@172.29.210.55/axis-cgi/mjpg/video.cgi",
            "http://root:Sintec1234@172.29.210.56/axis-cgi/mjpg/video.cgi",
            "http://root:Sintec1234@172.29.210.57/axis-cgi/mjpg/video.cgi",
            "http://root:Sintec1234@172.29.210.58/axis-cgi/mjpg/video.cgi",
            "http://root:Sintec1234@172.29.210.59/axis-cgi/mjpg/video.cgi",
            "http://root:Sintec1234@172.29.210.60/axis-cgi/mjpg/video.cgi"
        };
        //  Format RSTP fast but blur
        //            "rstp://root:admin@172.29.61.103:554/axis-media/media.amp?videocodec=h264",
        private VideoCapture[] _captureArr = new VideoCapture[CHANNELNUMBER];
        private volatile Mat[] _frameArr = new Mat[CHANNELNUMBER];
        private bool[] captureInProgress = new bool[CHANNELNUMBER];
        private bool[] showCamera = new bool[CHANNELNUMBER];
        private Semaphore smp_loadImg = new Semaphore(1, 1);
        private int previousCamera = 0;
        private int choseCamera = 1;
        private Semaphore smp_filter = new Semaphore(1, 1);

        public void CameraCapture(int i)
        {
            #region if capture is not created, create it now
            if (_captureArr[i] == null)
            {
                try
                {
                    _frameArr[i] = new Mat();
                    _captureArr[i] = new VideoCapture(site_all[i]);

                }

                catch (NullReferenceException excpt)
                {
                    MessageBox.Show(excpt.Message);
                }
            }
            if (_captureArr[i] != null)
            {
                try
                {
                    //How do i pass variables to a buttons event method?
                    //https://stackoverflow.com/questions/4815629/how-do-i-pass-variables-to-a-buttons-event-method
                    _captureArr[i].ImageGrabbed += (sender, EventArgs) => { ProcessFrame(sender, EventArgs, i); };
                    _captureArr[i].Start();

                    _cudafgDetectorArr[i] = new CudaBackgroundSubtractorMOG2();
                    //_fgDetectorArr[i] = new BackgroundSubtractorMOG2();
                    _blobDetectorArr[i] = new CvBlobDetector();
                    _trackerArr[i] = new CvTracks();
                    _tmpFrameArr[i] = new Mat();
                }

                catch (NullReferenceException excpt)
                {
                    MessageBox.Show(excpt.Message);
                }
            }
            #endregion
        }
        private void ProcessFrame(object sender, EventArgs e, int i)
        {
            try
            {
                if (_captureArr[i] != null && _captureArr[i].Ptr != IntPtr.Zero)
                {
                    _captureArr[i].Retrieve(_frameArr[i], 0);
                    //The methods/functions decode and return the just grabbed frame. If no frames has been grabbed (camera has been disconnected, or there are no more frames in video file), 
                    //the methods return false and the functions return NULL pointer.

                    CvInvoke.Resize(_frameArr[i], _frameArr[i], pictureboxWH, 0, 0, Inter.Linear);

                    if (isProcessMotion) //ENABLE OR DISABLE
                    {
                        CudaRun(i,Convert.ToInt32(cameraSensitivityLow[i]),Convert.ToInt32(cameraSensitivityHigh[i]));
                    }

                    smp_loadImg.WaitOne();
                    if (showCamera[i] == true)
                    {
                        this.Invoke(new Action(() => { pictureBox1.Image = _frameArr[i].Bitmap; }
                                    ));
                        //pictureBox1.Image = _frameArr[i].Bitmap;
                    }
                    smp_loadImg.Release();
                }
            }
            catch (NullReferenceException excpt)
            {
                MessageBox.Show(excpt.Message);
            }
        }
        //https://www.codeproject.com/Articles/257502/Creating-Your-First-EMGU-Image-Processing-Project
        #endregion
        #region highlightButton_load()
        private void highlightButton_load()
        {
            try
            {
                for (int i = 0; i < CHANNELNUMBER; i++)
                {
                    int localNum = i + 1;
                    string buttonName = "button_channel" + localNum;
                    //https://stackoverflow.com/questions/20773212/include-for-loop-in-button-name

                    if (_captureArr[i] != null)
                    {
                        tableLayoutPanel1.Controls[buttonName].BackColor = Color.FromArgb(128, 255, 128); //green
                    }
                    else
                    {
                        // Do not thing
                    }
                }
            }

            catch (NullReferenceException excpt)
            {
                MessageBox.Show(excpt.Message);
            }
        }
        #endregion

        //STEP 3.1 Click “LOAD CAMERA IMAGE”
        private void backgroundWorker_STEP3_DoWork(object sender, DoWorkEventArgs e)
        {
            try
            {
                for (int i = 0; i < CHANNELNUMBER; i++)
                {
                    int local = i; //local copies to avoid closure bugs
                    if (isConnect[local])
                    {
                        CameraCapture(local);
                    }
                    else
                    {
                        //Don notthing
                    }
                    Thread.Sleep(1000);
                    backgroundWorker_STEP3.ReportProgress(local + 1);

                }
            }

            catch (NullReferenceException excpt)
            {
                MessageBox.Show(excpt.Message);
            }
        }
        //3.2 Waiting Progress bar, the operation will complete in about two minutes or less.
        private void backgroundWorker_STEP3_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            try
            {
                int ix = e.ProgressPercentage;
                toolStripProgressBar1.Value = (ix * 100) / CHANNELNUMBER;
                toolStripStatusLabel_animate.Text = "Waiting... " + toolStripProgressBar1.Value + " %";
            }
            catch (NullReferenceException excpt)
            {
                MessageBox.Show(excpt.Message);
            }
        }
        //3.3 After finishing process, button changed back-color based on signal
        private void backgroundWorker_STEP3_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            try
            {
                Thread.Sleep(1000);
                toolStripStatusLabel_animate.Text = "Finish";
                highlightButton_load();

                button_cameraControl_checkConnection.Enabled = false;
                button_loadCameraImage.Enabled = false;
                button_enableMotion.Enabled = true;
                button_disableMotion.Enabled = false;
                //default click channel 1
                //you can trigger the button_click evnet from the code as follows:
                //Not now

                //load defualt chose camera
                string buttonName = "button_channel" + 1;
                button_camera_choose_Click(tableLayoutPanel1.Controls[buttonName], e);

                button_loadCameraImage.Enabled = false;
            }
            catch (NullReferenceException excpt)
            {
                MessageBox.Show(excpt.Message);
            }
        }

        #endregion

        #region STEP 4: TEST CAMERA DATA

        private void button_camera_choose_Click(object sender, EventArgs e)
        {
            try
            {
                //https://www.youtube.com/watch?v=zRBAz9g76e0
                //Test
                //MessageBox.Show((sender as Button).Text);
                choseCamera = int.Parse((sender as Button).Text);
                Console.WriteLine(choseCamera);
                //End Test

                //STEP 4.1 Click “CHANNEL NUMNER EX #1”
                #region Click “CHANNEL NUMNER EX #1”
                if (choseCamera == previousCamera)
                {
                    if(_captureArr[choseCamera-1] == null)
                    {
                        this.Invoke(new Action(() => { MessageBox.Show(this, "Can't Access to Camera"); }));
                        //MessageBox.Show("Can't Access to Camera");
                    }
                    else
                    {
                        this.Invoke(new Action(() => { MessageBox.Show(this, "Channel : " + choseCamera.ToString() + " Already Streaming"); }));
                        //MessageBox.Show("Channel : " + choseCamera.ToString() + " Already Streaming");
                    }
                }
                else
                {
                    Polygons.Clear();
                    //pictureBox1.Image = null;
                    //pictureBox1.Invalidate();

                    //STEP 4.2 CAMERA DISPLAY SHOW IMAGE FROM ACTIVE CAMERA
                    #region CAMERA DISPLAY SHOW IMAGE FROM ACTIVE CAMERA
                    smp_loadImg.WaitOne();
                    for (int i = 0; i < CHANNELNUMBER; i++)
                    {
                        showCamera[i] = false;
                    }
                    showCamera[choseCamera-1] = true;
                    previousCamera = choseCamera;
                    smp_loadImg.Release();
                    #endregion

                    //STEP 4.3 CAMERA PROPERTIES
                    #region Camera Properties
                    if (choseCamera < 10)
                    {
                        this.textBox_cameraProperties_channelNumber.Text = "0" + choseCamera.ToString();
                    }
                    else
                    {
                        this.textBox_cameraProperties_channelNumber.Text = choseCamera.ToString();
                    }
                    this.textBox_cameraProperties_locationName.Text = this.cameraLocation[choseCamera - 1];
                    this.textBox_cameraProperties_ipAddress.Text = this.cameraIPAddress[choseCamera - 1];


                    this.textBox_cameraProperties_loggingPath.Text = (Directory.GetCurrentDirectory() + "\\log_" + choseCamera.ToString() + ".txt").ToString();
                    //When using oin real
                    //this.textBox_cameraProperties_loggingPath.Text = @"C:\Program Files\REPCO\Intruder Detection System\log\channel" + choseCamera.ToString() + ".log";



                    if(cameraSensitivityLow[choseCamera-1] == "")
                    {
                        cameraSensitivityLow[choseCamera - 1] = "1";
                        cameraSensitivityLow[choseCamera - 1] = "5000";
                    }
                    this.trackBar_minimum.Value = Convert.ToInt32(cameraSensitivityLow[choseCamera - 1]);
                    this.trackBar_maximum.Value = Convert.ToInt32(cameraSensitivityHigh[choseCamera - 1]);
                    #endregion

                }
                #endregion
            }
            catch (NullReferenceException excpt)
            {
                MessageBox.Show(excpt.Message);
            }
        }

        #endregion

        #region STEP 5: SET DETECTION AREA

        //STEP 5.1 & 5.2 Draw restricted area by left click, then finish by right click on active camera.
        private void button_newAreaDetection_Click(object sender, EventArgs e)
        {
            try
            {
                config_load = false;
                config_reset = false;

                polygonArea_Now = null;
                for(int i=0;i< CHANNELNUMBER; i++)
                {
                    polygonAll[i] = null;
                }
                Polygons.Clear();
                config_start = true;
            }
            catch (NullReferenceException excpt)
            {
                MessageBox.Show(excpt.Message);
            }
        }
        //STEP 5.3  Click “SAVE AREA DETECTION”
        private void button_saveAreaDetection_Click(object sender, EventArgs e)
        {
            for(int i = 0; i < CHANNELNUMBER; i++)
            {
                if ((choseCamera-1) == i)
                {
                    ChangeChannelConfiguration(Directory.GetCurrentDirectory() + "\\config", "channel" + (i + 1).ToString("00"), i);
                }
            }
        }

        public string[] lineX { get; set; }
        public string[] lineY { get; set; }

        private void ChangeChannelConfiguration(string path, string name, int idCam)
        {
            // Config file path
            string filePath = path + '\\' + name + ".conf";


            try
            {
                if (polygonArea_Now == null)
                {
                    if (cameraPoint_x == null || cameraPoint_y == null)
                    {
                        MessageBox.Show("Please draw the cross-line Camera: " + (idCam + 1));
                    }
                    else
                    {
                        using (TextWriter tw = new StreamWriter(filePath))
                        {
                            // Write CHANNEL NUMBER
                            tw.WriteLine(name);
                            // Write LOCATION NAME
                            tw.WriteLine(textBox_cameraProperties_locationName.Text);
                            // Write IP ADDRESS
                            tw.WriteLine(textBox_cameraProperties_ipAddress.Text);


                            // Write area detection x
                            tw.WriteLine(this.cameraPoint_x[idCam]);

                            // Write area detection y
                            tw.WriteLine(this.cameraPoint_y[idCam]);


                            Console.WriteLine("New camera SensitivityLow camID:\t" + (idCam+1) + "\t" + cameraSensitivityLow[idCam]);
                            Console.WriteLine("new camera SensitivityHigh camID:\t" + (idCam+1) + "\t" + cameraSensitivityHigh[idCam]);

                            // Write camera senLow
                            tw.WriteLine(cameraSensitivityLow[idCam].ToString());

                            // Write camera senHigh
                            tw.WriteLine(cameraSensitivityHigh[idCam].ToString());

                            // Close config file
                            tw.Close();
                        }
                    }
                }
                else if (polygonArea_Now.Count() > 1)
                {
                    lineX = new string[polygonArea_Now.Count()];
                    lineY = new string[polygonArea_Now.Count()];

                    //string lineX; string lineY;
                    for (int i = 0; i < polygonArea_Now.Count(); i++)
                    {
                        lineX[i] = polygonArea_Now[i].X.ToString();
                        lineY[i] = polygonArea_Now[i].Y.ToString();
                    }

                    this.cameraPoint_x[idCam] = string.Join("|", lineX);
                    this.cameraPoint_y[idCam] = string.Join("|", lineY);



                    using (TextWriter tw = new StreamWriter(filePath))
                    {
                        // Write CHANNEL NUMBER
                        tw.WriteLine(name);
                        // Write LOCATION NAME
                        tw.WriteLine(textBox_cameraProperties_locationName.Text);
                        // Write IP ADDRESS
                        tw.WriteLine(textBox_cameraProperties_ipAddress.Text);


                        Console.WriteLine("new position x camID:\t" + (idCam+1) + "\t" + cameraPoint_x[idCam]);
                        Console.WriteLine("new position y camID:\t" + (idCam+1) + "\t" + cameraPoint_y[idCam]);

                        // Write area detection x
                        tw.WriteLine(this.cameraPoint_x[idCam]);

                        // Write area detection y
                        tw.WriteLine(this.cameraPoint_y[idCam]);

                        // Write camera senLow
                        tw.WriteLine(cameraSensitivityLow[idCam].ToString());

                        // Write camera senHigh
                        tw.WriteLine(cameraSensitivityHigh[idCam].ToString());

                        // Close config file
                        tw.Close();
                    }
                }
            }
            catch (NullReferenceException excpt)
            {
                MessageBox.Show(excpt.Message);
            }
        }


        //STEP 5.4 If already have restricted area in config file, then click “LOAD AREA DETECTION”
        private void button_loadAreaDetection_Click(object sender, EventArgs e)
        {
            config_load = true;
            config_start = false;

            //Clear Polygons for using Detection
            Polygons.Clear();
            polygonArea_Now = null;
            for (int i = 0; i < CHANNELNUMBER; i++)
            {
                polygonAll[i] = null;
            }

            int[] countPointPolygon = new int[CHANNELNUMBER];
            Array.Clear(countPointPolygon, 0, countPointPolygon.Length);

            for (int i = 0; i < CHANNELNUMBER; i++)
            {
                // Config file path
                string filePath = Directory.GetCurrentDirectory() + "\\config" + '\\' + "channel" + (i + 1).ToString("00") + ".conf";

                // Check config folder exist
                if (File.Exists(filePath))
                {
                    string[] lines_x = this.cameraPoint_x[i].Split(new char[] { '|' });
                    string[] lines_y = this.cameraPoint_y[i].Split(new char[] { '|' });

                    int count = lines_x.Count();
                    //Console.WriteLine(lines_x.Count());

                    if (polygonAll[i] == null)
                    {
                        polygonAll[i] = new Point[count];
                    }
                    while (countPointPolygon[i] < count)
                    {
                        polygonAll[i][countPointPolygon[i]].X = Int32.Parse(lines_x[countPointPolygon[i]]);
                        polygonAll[i][countPointPolygon[i]].Y = Int32.Parse(lines_y[countPointPolygon[i]]);
                        Console.WriteLine(polygonAll[i][countPointPolygon[i]].X.ToString() + "," + polygonAll[i][countPointPolygon[i]].Y.ToString());
                        countPointPolygon[i]++;
                    }
                    Polygons.Clear();
                }
            }
        }


        bool config_load = false;
        bool config_reset = false;
        bool config_start = false;
        public Point[] polygonArea_Now { get; set; }
        public Point[][] polygonAll = new Point[countButton][];
        private List<List<Point>> Polygons = new List<List<Point>>();
        private List<Point> NewPolygon = new List<Point>();
        private Point NewPoint = new Point();

        #region polygons
        private void picCanvas_MouseDown(object sender, MouseEventArgs e)
        {
            try
            {
                if (config_start == true && config_reset == false)
                {
                    if (NewPolygon != null)
                    {
                        if (e.Button == MouseButtons.Right)
                        {
                            if (NewPolygon.Count > 2)
                            {
                                Polygons.Add(NewPolygon);
                                config_reset = true;
                            }
                            NewPolygon = null;
                        }
                        else
                        {
                            if (NewPolygon[NewPolygon.Count - 1] != e.Location)
                            {
                                NewPolygon.Add(e.Location);
                            }
                        }
                    }
                    else
                    {
                        NewPolygon = new List<Point>();
                        NewPoint = e.Location;
                        NewPolygon.Add(e.Location);
                    }
                    //pictureBox1.Image = null;
                    //pictureBox1.Invalidate();
                }
            }
            catch (NullReferenceException excpt)
            {
                MessageBox.Show(excpt.Message);
            }
        }
        private void picCanvas_MouseMove(object sender, MouseEventArgs e)
        {
            try
            {
                if (config_start == true)
                {
                    if (NewPolygon == null) return;

                    NewPoint = e.Location;
                    //pictureBox1.Image = null;
                    //pictureBox1.Invalidate();
                }
            }
            catch (NullReferenceException excpt)
            {
                MessageBox.Show(excpt.Message);
            }
        }
        private void picCanvas_Paint(object sender, PaintEventArgs e)
        {
            try
            {
                if (config_load == true)
                {
                    for(int i=0;i < CHANNELNUMBER; i++)
                    {
                        if((choseCamera-1) == i)
                        {
                            if (polygonAll[i].Count() <= 1)
                                return;
                            else
                            {
                                Graphics l = e.Graphics;
                                Pen p = new Pen(Color.Yellow, 1);
                                Point[] a = polygonAll[i];
                                l.DrawPolygon(p, a);
                            }
                        }
                    }
                }
                if(config_start == true)
                {
                    foreach(List<Point> polygon in Polygons)
                    {
                        e.Graphics.DrawPolygon(Pens.Yellow, polygon.ToArray());
                        polygonArea_Now = polygon.ToArray();
                    }
                    if(NewPolygon != null)
                    {
                        if (NewPolygon.Count > 1)
                        {
                            e.Graphics.DrawLines(Pens.LightBlue, NewPolygon.ToArray());
                        }
                        if (NewPolygon.Count > 0)
                        {
                            using(Pen dashed_pen = new Pen(Color.LightBlue))
                            {
                                dashed_pen.DashPattern = new float[] { 3, 3 };
                                e.Graphics.DrawLine(dashed_pen,
                                    NewPolygon[NewPolygon.Count - 1],
                                    NewPoint);
                            }
                        }
                    }
                }
            }
            catch (NullReferenceException excpt)
            {
                MessageBox.Show(excpt.Message);
            }
        }
        private bool PointInPolygon(Point[] polygon, int x, int y)
        {

            if (polygon == null || polygon.Length < 3) return false;
            int counter = 0;
            double intersections;
            Point p1 = polygon[0];
            Point p2 = polygon[0];
            for (int i = 1; i <= polygon.Length; i++)
            {

                p2 = polygon[i % polygon.Length];
                if ((y > (p1.Y < p2.Y ? p1.Y : p2.Y)) && (y <= (p1.Y > p2.Y ? p1.Y : p2.Y)) && (x <= (p1.X > p2.X ? p1.X : p2.X)) && (p1.Y != p2.Y))
                {
                    intersections = (y - p1.Y) * (p2.X - p1.X) / (p2.Y - p1.Y) + p1.X;
                    if (p1.X == p2.X || x <= intersections) counter++;
                }
                p1 = p2;
            }

            return counter % 2 != 0;
        }

        private void picCanvas_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            try
            {
                if (config_start == true)
                {
                    if (e.Button == MouseButtons.Right)
                    {
                        int mouseX = e.X;
                        int mouseY = e.Y;
                        try
                        {
                            string point_all = "";
                            for (int i = 0; i < polygonAll.Length; i++)
                            {
                                point_all += polygonAll[i] + "\n";
                            }
                        }
                        catch
                        {
                            this.Invoke(new Action(() => { MessageBox.Show(this, "Click another"); }));
                        }
                    }
                }
            }
            catch (NullReferenceException excpt)
            {
                MessageBox.Show(excpt.Message);
            }
        }
        #endregion

        #endregion

        #region STEP 6: START PROCESS

        private bool isProcessMotion = false;

        //Normal
        //private BackgroundSubtractorMOG2[] _fgDetectorArr = new BackgroundSubtractorMOG2[CHANNELNUMBER];

        //Cuda detection motion
        private CudaBackgroundSubtractorMOG2[] _cudafgDetectorArr = new CudaBackgroundSubtractorMOG2[CHANNELNUMBER];
        private CvBlobDetector[] _blobDetectorArr = new CvBlobDetector[CHANNELNUMBER];
        private CvTracks[] _trackerArr = new CvTracks[CHANNELNUMBER];
        private CvBlobs[] blobs = new CvBlobs[CHANNELNUMBER];

        //Result image
        private Mat[] _tmpFrameArr = new Mat[CHANNELNUMBER];
        private Mat[] _forgroundMaskArr = new Mat[CHANNELNUMBER];
        private GpuMat[] _currentImageArr = new GpuMat[CHANNELNUMBER];
        private GpuMat[] _oldImageArr = new GpuMat[CHANNELNUMBER];
        private float[] scale = new float[CHANNELNUMBER];

        //Signal Alarm
        private static bool[] signalAlarm = new bool[CHANNELNUMBER];
        private string[] buttonName = new string[CHANNELNUMBER];

        //STEP 6.1  Click “ENABLE MOTION DETECTION”
        private void button_enableMotion_Click(object sender, EventArgs e)
        {
            button_cameraControl_checkConnection.Enabled = false;
            button_loadCameraImage.Enabled = false;
            button_enableMotion.Enabled = false;
            button_disableMotion.Enabled = true;


            button_loadAreaDetection_Click(sender, e);
            isProcessMotion = true;
            ResetBtn();


        }
        //STEP 6.2  Click “DISABLE MOTION DETECTION”
        private void button_disableMotion_Click(object sender, EventArgs e)
        {

            button_cameraControl_checkConnection.Enabled = true;
            button_loadCameraImage.Enabled = false;
            button_enableMotion.Enabled = true;
            button_disableMotion.Enabled = false;

            isProcessMotion = false;
            ResetBtn();

        }

        private void ResetBtn()
        {
            try
            {
                for (int i = 0; i < CHANNELNUMBER; i++)
                {
                    if (isConnect[i] == true)
                    {
                        buttonName[i] = "button_channel" + (i + 1).ToString();
                        if (isProcessMotion == true)
                        {
                            tableLayoutPanel1.Controls[buttonName[i]].Invoke(new MethodInvoker(delegate { tableLayoutPanel1.Controls[buttonName[i]].BackColor = Color.Green; }));
                        }
                        else
                        {
                            tableLayoutPanel1.Controls[buttonName[i]].Invoke(new MethodInvoker(delegate { tableLayoutPanel1.Controls[buttonName[i]].BackColor = Color.FromArgb(128, 255, 128); }));
                        }
                    }
                }
            }
            catch (NullReferenceException excpt)
            {
                MessageBox.Show(excpt.Message);
            }
        }

        private void CudaRun(int camID, int lowFilter, int highFilter)
        {
            //New add
            //minimizeMemory();

            if (camID >= CHANNELNUMBER)
            {
                return;
            }
            try
            {
                //_tmpFrameArr[camID] = _frameArr[camID];
                _tmpFrameArr[camID] = _frameArr[camID].Clone();

                buttonName[camID] = "button_channel" + (camID + 1).ToString();

                if (_tmpFrameArr[camID] != null)
                {
                    ////OLD
                    //tableLayoutPanel1.Controls[buttonName[camID]].BackColor = Color.Green;
                    //you need to use Invoke/BeginInvoke to forward updated to the GUI thread:
                    tableLayoutPanel1.Controls[buttonName[camID]].Invoke(new MethodInvoker(delegate { tableLayoutPanel1.Controls[buttonName[camID]].BackColor = Color.Green; }));



                    using (GpuMat tmp = new GpuMat(_tmpFrameArr[camID]))
                    {
                        if (_oldImageArr[camID] == null)
                        {

                            //Mat bgrFrame = _videoCaptureArr[camID].QueryFrame();
                            using (GpuMat oldBgrImage = new GpuMat(_frameArr[camID]))
                            {
                                _oldImageArr[camID] = new GpuMat();
                            //    CudaInvoke.CvtColor(oldBgrImage, _oldImageArr[camID], ColorConversion.Bgr2Gray);
                                CudaInvoke.CvtColor(oldBgrImage, _oldImageArr[camID], ColorConversion.Bgr2Gray);
                            }
                        }

                        _currentImageArr[camID] = new GpuMat();
                        CudaInvoke.CvtColor(tmp, _currentImageArr[camID], ColorConversion.Bgr2Gray);

                        // Set oldImage >> foreground
                        _cudafgDetectorArr[camID].Apply(_currentImageArr[camID], _oldImageArr[camID]);
                        _forgroundMaskArr[camID] = new Mat();
                        _oldImageArr[camID].Download(_forgroundMaskArr[camID]);

                        blobs[camID] = new CvBlobs();
                        _blobDetectorArr[camID].Detect(_forgroundMaskArr[camID].ToImage<Gray, byte>(), blobs[camID]);

                        blobs[camID].FilterByArea(lowFilter, highFilter);
                        //blobs.FilterByArea(100, 5000);

                        scale[camID] = (_tmpFrameArr[camID].Width + _tmpFrameArr[camID].Width) / 2.0f;
                        //_trackerArr[camID].Update(blobs, 0.01 * scale, 5, 5);
                        _trackerArr[camID].Update(blobs[camID], 1000 * scale[camID], 1, 1);

                        foreach (var pair in _trackerArr[camID])
                        {
                            CvTrack b = pair.Value;
                            if (PointInPolygon(polygonAll[camID], b.BoundingBox.X + (b.BoundingBox.Width / 2), b.BoundingBox.Y + (b.BoundingBox.Height / 2)) == true || PointInPolygon(polygonArea_Now, b.BoundingBox.X + (b.BoundingBox.Width / 2), b.BoundingBox.Y + (b.BoundingBox.Height / 2)) == true && (choseCamera - 1) == camID)
                            {
                                //if ((choseCamera - 1) == camID)
                                //{
                                    CvInvoke.Rectangle(_tmpFrameArr[camID], b.BoundingBox, new MCvScalar(0.0, 0.0, 255.0), 2);
                                //}                      
                                //log.Add(DateTime.Now + "," + site_all_ip[camID]);
                                //string result = string.Join("|", log.ToArray());
                                ////Stopwatch s = Stopwatch.StartNew();
                                //File.AppendAllText("log_" + camID + ".txt", result);
                                ////s.Stop(); //Time < 30 ms * 40 IDcam = 1.2 Seconds // Thus, Stamped time every 2 seconds
                                ////Console.WriteLine("Elasped {0}:  {1}",camID,s.ElapsedMilliseconds);
                                ///
                                signalAlarm[camID] = true;
                                ////OLD
                                //tableLayoutPanel1.Controls[buttonName[camID]].BackColor = Color.Red;
                                //you need to use Invoke/BeginInvoke to forward updated to the GUI thread:
                                tableLayoutPanel1.Controls[buttonName[camID]].Invoke(new MethodInvoker(delegate { tableLayoutPanel1.Controls[buttonName[camID]].BackColor = Color.Red; }));
                            }
                            else
                            {
                                signalAlarm[camID] = false;
                            }
                        }
                        _oldImageArr[camID] = _currentImageArr[camID];
                    }
                }
            }
            catch (NullReferenceException excpt)
            {
                MessageBox.Show(excpt.Message);
            }
        }

        #endregion


        private void trackBar_minimum_ValueChanged(object sender, EventArgs e)
        {
           this.Invoke(new MethodInvoker(delegate { textBoxMinFilter.Text = trackBar_minimum.Value.ToString(); }));

            cameraSensitivityLow[choseCamera - 1] = trackBar_minimum.Value.ToString();
        }

        private void trackBar_maximum_ValueChanged(object sender, EventArgs e)
        {
           this.Invoke(new MethodInvoker(delegate { textBoxMaxFilter.Text = trackBar_maximum.Value.ToString(); }));

            cameraSensitivityHigh[choseCamera - 1] = trackBar_maximum.Value.ToString();
        }

        private void buttonOpenlog_Click(object sender, EventArgs e)
        {
            Process.Start("notepad.exe", textBox_cameraProperties_loggingPath.Text);
        }

        private void FormMain_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (Application.MessageLoop)
            {
                // WinForms app
                Application.Exit();
            }
            else
            {
                // Console app
                Environment.Exit(1);
            }
        }
    }
}