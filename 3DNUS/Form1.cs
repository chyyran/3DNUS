using System;
using System.IO;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Net;
using System.Diagnostics;

namespace _3DNUS
{
    public partial class Main : Form
    {
        string server = "http://nus.cdn.c.shop.nintendowifi.net/ccs/download/";
        List<string> skipped = new List<string>();
        public Main()
        {
            InitializeComponent();
        }

        private void c_spoof_CheckedChanged(object sender, EventArgs e)
        {
            if (c_spoof.Checked == true)
            {
                t_spoof.Enabled = true;
            }
            if (c_spoof.Checked == false)
            {
                t_spoof.Enabled = false;
            }
        }
        private void b_download_Click(object sender, EventArgs e)
        {
            l_error.Visible = false;
            p_progress.Value = 0;

            if(t_titleid.Text.Length ==0)
            {
                MessageBox.Show("No TitleID entered");
                return;
            }
            if(t_version.Text.Length == 0)
            {
                MessageBox.Show("No version number entered");
                return;
            }
            if(c_spoof.Checked && t_spoof.Text.Length == 0)
            {
                MessageBox.Show("No spoofing version entered"); 
                return;
            }

            if (c_spoof.Checked)
            {
                int check;
                try
                {
                    if (t_spoof.Text == "+" && t_titleid.Text.Contains("."))
                    {
                    }
                    else
                    {
                        check = int.Parse(t_spoof.Text);
                        if (check > 65535)
                        {
                            MessageBox.Show("Spoofing version too high, please enter a number under '65536' or '+'(when downloading a firmware)");
                            return;
                        }
                    }
                }
                catch
                {
                    MessageBox.Show("No valid spoof number entered");
                    return;
                }
            }
   
            if (t_titleid.Text.Contains("."))
            {
                string firmw = t_titleid.Text;
                string reg = t_version.Text;
                log(0, "Downloading Firmware: " + firmw + reg);
                Cursor = Cursors.WaitCursor;
                p_input.Enabled = false;
                BackgroundWorker fd = new BackgroundWorker();
                fd.DoWork += (obj, r) => firmwdownload(firmw, reg);
                fd.RunWorkerAsync();
                
            }
            else
            {
                string spoof;
                string title = t_titleid.Text;
                string version = t_version.Text;
                Cursor = Cursors.WaitCursor;
                p_input.Enabled = false;
                if (c_spoof.Checked)
                {
                    spoof = t_spoof.Text;
                }
                else
                {
                    spoof = "4444";
                }
                BackgroundWorker sf = new BackgroundWorker();
                sf.DoWork += (obj, r) => singledownload(title, version, spoof);
                sf.RunWorkerAsync();
            }
        }

        private void singledownload(string title, string version, string spoof)
        {
            log(2,"Downloading " + title + " v" + version);
            string cd = Path.GetDirectoryName(Application.ExecutablePath);
            string ftmp = cd + "\\tmp";
            string downloadtmd = server + title + "/" + "tmd." + version;
            string downloadcetk = server + title + "/cetk";
            if (!t_titleid.Text.Contains("."))
            {
                p_progress.Parent.Invoke(new MethodInvoker(delegate { p_progress.Maximum = 10; }));
            }

            try
            {
                Directory.Delete(ftmp, true);
            }
            catch { }
            Directory.CreateDirectory(ftmp);

            try
            {
                WebClient dtmd = new WebClient();
                dtmd.DownloadFile(downloadtmd, @ftmp + "\\tmd");
                dtmd.DownloadFile(downloadcetk, @ftmp + "\\cetk");
            }
            catch
            {
                log(2, "Error downloading title " + title + " v" + version + " make sure the entered title ID and versions are correct.");              
                if (!t_titleid.Text.Contains("."))
                {
                    p_input.Enabled = true;
                    Cursor = Cursors.Default;                 
                }
                return;

            }
            if (!t_titleid.Text.Contains("."))
            {
                p_progress.Parent.Invoke(new MethodInvoker(delegate { p_progress.Value = p_progress.Value + 3; }));
            }
            //amount of contents
            FileStream tmd = File.Open(ftmp + "\\tmd", FileMode.Open, FileAccess.ReadWrite);
            tmd.Seek(518, SeekOrigin.Begin);
            byte[] cc = new byte[2];
            tmd.Read(cc, 0, 2);
            Array.Reverse(cc);
            int contentcounter = BitConverter.ToInt16(cc, 0);
            log(1,"Title has " + contentcounter + " contents.");

            //download files           
            WebClient contd = new WebClient();
            for (int i = 1; i <= contentcounter; i++)
            {
                log(1,"Downloading file" + i.ToString() + "...");
                int contentoffset = 2820 + (48 * (i - 1));
                tmd.Seek(contentoffset, SeekOrigin.Begin);
                byte[] cid = new byte[4];
                tmd.Read(cid, 0, 4);
                string contentid = BitConverter.ToString(cid).Replace("-", "");
                string downname = ftmp + "\\" + contentid;
                try
                {
                    contd.DownloadFile(server + title + "/" + contentid, @downname);
                }
                catch (WebException e)
                {
                    log(0, "Error " + ((HttpWebResponse)e.Response).StatusCode + " when downloading " + title + "/" + contentid + ". SKIPPING");
                    tmd.Close();
                    this.skipped.Add(title + "/" + contentid);
                    return;
                }
                log(0," complete.");
            }
            if (!t_titleid.Text.Contains("."))
            {
                p_progress.Parent.Invoke(new MethodInvoker(delegate { p_progress.Value = p_progress.Value + 2; }));
            }
            //change version number          
            if (c_spoof.Checked)
            {
                int fws = int.Parse(spoof);
                string hex = fws.ToString("X");                
                log(1, "changing the version number to: " + spoof + "("+ hex + ")");

                byte[] nv = tobyte(hex);
                tmd.Seek(476, SeekOrigin.Begin);
                tmd.WriteByte(nv[0]);
                tmd.WriteByte(nv[1]);
             
            }
            if (!t_titleid.Text.Contains("."))
            {
                p_progress.Parent.Invoke(new MethodInvoker(delegate { p_progress.Value = p_progress.Value + 1; }));
            }
            tmd.Close();
            if (c_cia.Checked)
            {
                //create cia
                log(1,"Packing as .cia.");
                string command;
                if (t_titleid.Text.Contains("."))
                {
                    command = " " + "tmp" + " " + t_titleid.Text + "\\" + title + ".cia";
                }
                else
                {
                    command = " " + "tmp" + " " + title + ".cia";
                }
                Process create = new Process();
                create.StartInfo.FileName = "make_cdn_cia.exe";
                create.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
                create.StartInfo.Arguments = command;
                create.Start();
                create.WaitForExit();
                Directory.Delete(ftmp, true);
            }
            else
            {
                if (t_titleid.Text.Contains("."))
                {
                    if (Directory.Exists(cd + "\\" + t_titleid.Text + "\\" + title))
                    {
                        Directory.Delete(cd + "\\" + t_titleid.Text + "\\" + title, true);
                    }
                    Directory.Move(ftmp, cd + "\\" + t_titleid.Text + "\\" + title);
                }
                else
                {   
                    if (Directory.Exists(cd + "\\" + title + "v" + version))
                    {
                        Directory.Delete(cd + "\\" + title + "v" + version, true);
                    }
                    Directory.Move(ftmp, cd + "\\" + title + "v" + version);
                }

            }
            log(1,"Done.");
            if (!t_titleid.Text.Contains("."))
            {
                p_progress.Parent.Invoke(new MethodInvoker(delegate { p_progress.Value = p_progress.Value + 4; }));
            }
            if (!t_titleid.Text.Contains("."))
            {
                p_input.Invoke((MethodInvoker)(() => p_input.Enabled = true));
                Cursor = Cursors.Default;
            }
        }
        private void firmwdownload(string firm, string reg)
        {

            string spoof;
            string[] titles = null;
            string cd = Path.GetDirectoryName(Application.ExecutablePath);
            if (String.IsNullOrWhiteSpace(this.openFileDialog1.SafeFileName))
            {
                try
                {
                    WebClient titlelist = new WebClient();
                    titlelist.DownloadFile("http://yls8.mtheall.com/ninupdates/titlelist.php?sys=ctr&csv=1", cd + "\\titlelist.csv");
                    log(2, "Downloading titlelist complete");
                    titles = File.ReadAllLines(cd + "\\titlelist.csv");

                }
                catch
                {
                    log(2, "Error downloading titlelist");
                    if (File.Exists(cd + "\\titlelist.csv"))
                    {
                        log(1, "Using existing titlelist, this maybe not up to date");
                    }
                    else
                    {
                        log(1, "Cant find an existing titlelist, this program quits");
                        return;
                    }
                }
            }
            else 
            {
                titles = File.ReadAllLines(this.openFileDialog1.FileName);
                log(1, "Using local titlelist " + this.openFileDialog1.FileName);
            }


            p_progress.Parent.Invoke(new MethodInvoker(delegate { p_progress.Maximum = (titles.Length); }));
            Directory.CreateDirectory(cd + "\\" + t_titleid.Text);
            foreach (string select1 in titles.Skip(1))
            {
                if (select1.Contains(reg))
                {
                    string title;
                    string version;

                    int[] wantedfw = Array.ConvertAll(t_titleid.Text.Replace(".", "").Split('-'),int.Parse);
                    string[] csv = select1.Split(',');
                    string firmwaresls = csv[3].Replace(" Initial scan", "").Replace(" (stage1)", "").Replace(" (stage2)", "").Replace(" (stage3)", "").Replace(" (stage4)", "").Replace(" (stage5)", "").Replace(" (stage6)", "").Replace(" (stage7)", "").Replace("E", "").Replace("U", "").Replace("J", "");
                    string[] csvfirm = firmwaresls.Split(' ');
                    int[] csvfu = Array.ConvertAll(csvfirm[0].Replace(".", "").Split('-'),int.Parse);

                    if (wantedfw[1]>=csvfu[1]&&wantedfw[0]>=csvfu[0])
                    {
                        string use = null;
                        foreach (string temp in csvfirm)
                        {
                            string currentclean = temp;                           
                            int[] intcc = Array.ConvertAll(currentclean.Replace(".", "").Split('-'), int.Parse);
                           
                            if (wantedfw[0]<intcc[0]&&wantedfw[1]<intcc[1])
                            {
                                break;
                            }
                            use = currentclean;  
                        }
                        //set download title
                        title = csv[0];
                        // find version number
                        int verindex = Array.IndexOf(csvfirm, use);
                        // get version number
                        string[] aver = csv[2].Split(' ');
                        if (csv[2].Contains(" "))
                        {                            
                            version = aver[verindex].Replace("v", "");
                        }
                        else
                        {
                            version = csv[2].Replace("v", "");
                        }
                        //set the spoof version
                        if (c_spoof.Checked)
                        {
                            if (t_spoof.Text == "+")
                            {
                                spoof = aver.Last();
                                spoof = spoof.Replace("v", "");
                                int tmspoof = int.Parse(spoof);
                                tmspoof = tmspoof + 1;
                                spoof = tmspoof.ToString();
                            }
                            else
                            {
                                spoof = t_spoof.Text;
                            }
                        }
                        else
                        {
                            spoof = "4444";
                        }
                        //send the command
                        singledownload(title, version, spoof);                        
                    }
                }
                p_progress.Parent.Invoke(new MethodInvoker(delegate { p_progress.Value = p_progress.Value + 1; }));
            }
            log(2, "Downloading firmware complete!");
            foreach(string warn in skipped)
            {
                log(2, "WARNING SKIPPED " + warn);
            }
            p_input.Invoke((MethodInvoker)(() => p_input.Enabled = true));
            Cursor = Cursors.Default;
        }
        private void log(int nl, string msg)
        {
               for (int i = 0; i < nl; i++)
                {
                    t_log.Invoke((MethodInvoker)(() => t_log.AppendText("\r\n")));
                }
               t_log.Invoke((MethodInvoker)(() =>t_log.AppendText(msg)));
               if(msg.Contains("Error"))
               {
                   l_error.Invoke((MethodInvoker)(() => l_error.Visible = true));
               }
          }
        public static byte[] tobyte(string hex)
        {
            Dictionary<string, byte> hexindex = new Dictionary<string, byte>();
            for (int i = 0; i <= 255; i++)
                hexindex.Add(i.ToString("X2"), (byte)i);
            
            while (hex.Length <4)
            {
                hex = "0" + hex;
            }
            List<byte> hexres = new List<byte>();
            for (int i = 0; i < hex.Length; i += 2)
                hexres.Add(hexindex[hex.Substring(i, 2)]);

            return hexres.ToArray();
        }

        private void label1_Click(object sender, EventArgs e)
        {

        }

        private void button1_Click(object sender, EventArgs e)
        {
            this.openFileDialog1.ShowDialog();
        }
    }
    }

