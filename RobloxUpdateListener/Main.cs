using System;
using System.Collections.Generic;
using System.Net;
using System.Windows.Forms;

using Microsoft.Win32;

namespace RobloxUpdateListener
{
    public struct QueuedNotification
    {
        public string Title;
        public string Message;
    }

    public partial class Main : Form
    {
        private static RegistryKey registry = Registry.CurrentUser.CreateSubKey("Software\\RobloxUpdateListener");
        private static string apiKey = "76e5a40c-3ae1-4028-9f10-7c62520bd94f";

        private static WebClient http = new WebClient();

        private static Dictionary<string,string> branches = new Dictionary<string,string>()
        {
            { "roblox",               "Production"  },
            { "gametest1.robloxlabs", "Trunk"       },
            { "gametest2.robloxlabs", "Integration" }
        };

        private static List<string> binaryTypes = new List<string>()
        {
            "WindowsPlayer", "WindowsStudio",
            "MacPlayer",     "MacStudio",

            "RCCService"
        };

        private List<QueuedNotification> queue = new List<QueuedNotification>();
        private bool queueLock = false;

        protected override void OnLoad(EventArgs e)
        {
            Visible = false;
            ShowInTaskbar = false;
            Opacity = 0;

            base.OnLoad(e);
        }

        private void stepQueue()
        {
            if (!queueLock && queue.Count > 0)
            {
                queueLock = true;

                QueuedNotification notification = queue[0];
                notifier.ShowBalloonTip(5000, notification.Title, notification.Message, ToolTipIcon.Info);

                queue.RemoveAt(0);
            }
        }

        private void freeLockAndStep()
        {
            queueLock = false;
            stepQueue();
        }

        private void notifier_BalloonTipClosed(object sender, EventArgs e)
        {
            freeLockAndStep();
        }

        private void notifier_BalloonTipClicked(object sender, EventArgs e)
        {
            freeLockAndStep();
        }

        private async void checkForUpdate()
        {
            foreach (string branch in branches.Keys)
            {
                RegistryKey regBranch = registry.CreateSubKey(branch);
                string formalName = branches[branch];

                foreach (string binaryType in binaryTypes)
                {
                    string url = "https://versioncompatibility.api." + branch + ".com/GetCurrentClientVersion/?apiKey=" + apiKey + "&binaryType=" + binaryType;
                    string oldVersion = regBranch.GetValue(binaryType) as string;
                    string newVersion = await http.DownloadStringTaskAsync(url);
                    newVersion = newVersion.Replace("\"", "");

                    if (oldVersion != newVersion)
                    {
                        QueuedNotification notification = new QueuedNotification();
                        notification.Title = "New " + binaryType + " deployed to " + formalName + "!";
                        notification.Message = newVersion + "\n(" + branch + ")";
                        queue.Add(notification);

                        regBranch.SetValue(binaryType, newVersion);
                        stepQueue();
                    }
                }
            }
        }

        private void onTick(object sender, EventArgs e)
        {
            checkForUpdate();
        }

        public Main()
        {
            InitializeComponent();
            checkForUpdate();

            timer.Interval = 30000;
            timer.Tick += new EventHandler(onTick);
            timer.Start();
        }
    }
}
