﻿using System;
using System.Windows.Forms;

namespace KinectProject
{
    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new Form1()); // Launch the MainForm
        }
    }
}
