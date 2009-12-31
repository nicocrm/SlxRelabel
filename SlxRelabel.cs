using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.IO;
using System.Xml.Linq;
using System.Xml;

/**********************************************************************************
    Copyright 2009 Nicolas Galler 
 
    This program is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with this program.  If not, see <http://www.gnu.org/licenses/>.
 **********************************************************************************/



namespace SlxRelabel
{
    /// <summary>
    /// SlxRelabel - edit resx files through a SalesLogix project to change a label.
    /// </summary>
    public partial class SlxRelabel : Form
    {
        public SlxRelabel()
        {
            InitializeComponent();
        }

        private void label1_Click(object sender, EventArgs e)
        {

        }

        private void btnRelabel_Click(object sender, EventArgs e)
        {
            FormDto dto = new FormDto { ModelRoot = txtModelRoot.Text, From = txtFrom.Text, To = txtTo.Text };
            if (txtModelRoot.Text == "" || txtFrom.Text == "" || txtTo.Text == "")
            {
                MessageBox.Show("Missing parameter");
                return;
            }
            btnCancel.Enabled = true;
            btnRelabel.Enabled = false;
            backgroundWorker1.RunWorkerAsync(dto);
        }

        private class FormDto
        {
            public String ModelRoot;
            public String From;
            public String To;
        }

        private void backgroundWorker1_DoWork(object sender, DoWorkEventArgs e)
        {
            FormDto formData = (FormDto) e.Argument;
            if (!File.Exists(Path.Combine(formData.ModelRoot, "project.info.xml")))
            {
                throw new ArgumentException("Specified path does not appear to be a SalesLogix project (missing project.info.xml).");
            }
            List<String> modifiedFiles = new List<string>();
            DoRelabel(new DirectoryInfo(formData.ModelRoot), formData.From, formData.To, modifiedFiles);
            e.Result = String.Join("\n", modifiedFiles.ToArray());
        }

        private void DoRelabel(DirectoryInfo directoryInfo, string from, string to, List<String> modifiedFiles)
        {
            foreach (FileInfo f in directoryInfo.GetFiles("*.resx"))
            {
                if (RelabelFile(f, from, to))
                    modifiedFiles.Add(f.FullName);
                if (backgroundWorker1.CancellationPending)
                    return;
            }
            foreach (DirectoryInfo d in directoryInfo.GetDirectories())
            {
                DoRelabel(d, from, to, modifiedFiles);
                if (backgroundWorker1.CancellationPending)
                    return;
            }
        }

        /// <summary>
        /// Run relabel in file.  Return true if there was an actual change.
        /// </summary>
        /// <param name="f"></param>
        /// <param name="from"></param>
        /// <param name="to"></param>
        /// <returns></returns>
        private bool RelabelFile(FileInfo f, string from, string to)
        {
            bool hasChange = false;
            XDocument resDoc = XDocument.Load(f.FullName);
            foreach (XElement valueNode in resDoc.Element("root")
                .Descendants("data")
                .Where(e => e.Attribute("type") == null || e.Attribute("type").Value == "String")
                .Descendants("value"))
            {
                String newValue = valueNode.Value.Replace(from, to);
                if (newValue != valueNode.Value)
                {
                    hasChange = true;
                    valueNode.Value = newValue;
                }
            }
            if (hasChange)
            {
                using (var w = XmlWriter.Create(f.FullName, new XmlWriterSettings() { Indent = true }))
                {                    
                    resDoc.WriteTo(w);
                    return true;
                }                
            }
            return false;
        }


        private void backgroundWorker1_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            prg.Value = e.ProgressPercentage;
        }

        private void backgroundWorker1_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            btnCancel.Enabled = false;
            btnRelabel.Enabled = true;
            prg.Visible = false;
            
            if (e.Cancelled)
            {
                MessageBox.Show("Process was cancelled by user.  Some changes may have already been saved.", "SlxRelabel", MessageBoxButtons.OK, MessageBoxIcon.Information);
                txtResults.Text = "";
            }
            else if (e.Error != null)
            {
                Console.Error.WriteLine(e.Error.StackTrace);
                txtResults.Text = e.Error.StackTrace;
                MessageBox.Show("An error occurred: " + e.Error.Message, "SlxRelabel", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            else
            {                
                MessageBox.Show("Process Completed Successfully.", "SlxRelabel", MessageBoxButtons.OK, MessageBoxIcon.Information);
                txtResults.Text = (String)e.Result;
            }
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            backgroundWorker1.CancelAsync();
        }


    }
}
