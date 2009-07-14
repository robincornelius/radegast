// 
// Radegast Metaverse Client
// Copyright (c) 2009, Radegast Development Team
// All rights reserved.
// 
// Redistribution and use in source and binary forms, with or without
// modification, are permitted provided that the following conditions are met:
// 
//     * Redistributions of source code must retain the above copyright notice,
//       this list of conditions and the following disclaimer.
//     * Redistributions in binary form must reproduce the above copyright
//       notice, this list of conditions and the following disclaimer in the
//       documentation and/or other materials provided with the distribution.
//     * Neither the name of the application "Radegast", nor the names of its
//       contributors may be used to endorse or promote products derived from
//       this software without specific prior written permission.
// 
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS"
// AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE
// IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
// DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT HOLDER OR CONTRIBUTORS BE LIABLE
// FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL
// DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR
// SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER
// CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY,
// OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE
// OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
//
// $Id$
//
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Text;
using System.Windows.Forms;
using Radegast.Netcom;
using OpenMetaverse;

namespace Radegast
{
    public partial class frmProfile : Form
    {
        private RadegastInstance instance;
        private RadegastNetcom netcom;
        private GridClient client;
        private string fullName;
        private UUID agentID;

        private UUID FLImageID;
        private UUID SLImageID;

        public frmProfile(RadegastInstance instance, string fullName, UUID agentID)
        {
            InitializeComponent();
            Disposed += new EventHandler(frmProfile_Disposed);

            // Picks tab is not made yet
            tabProfile.TabPages.Remove(tbpPicks);

            this.instance = instance;
            netcom = this.instance.Netcom;
            client = this.instance.Client;
            this.fullName = fullName;
            this.agentID = agentID;
            
            this.Text = fullName + " (profile) - TheLBot";

            // Callbacks
            client.Avatars.OnAvatarNames += new AvatarManager.AvatarNamesCallback(Avatars_OnAvatarNames);
            client.Avatars.OnAvatarProperties += new AvatarManager.AvatarPropertiesCallback(Avatars_OnAvatarProperties);
            client.Avatars.OnAvatarPicks += new AvatarManager.AvatarPicksCallback(Avatars_OnAvatarPicks);
            client.Avatars.OnPickInfo += new AvatarManager.PickInfoCallback(Avatars_OnPickInfo);
            netcom.ClientLoggedOut += new EventHandler(netcom_ClientLoggedOut);

            InitializeProfile();
        }

        void frmProfile_Disposed(object sender, EventArgs e)
        {
            client.Avatars.OnAvatarNames -= new AvatarManager.AvatarNamesCallback(Avatars_OnAvatarNames);
            client.Avatars.OnAvatarProperties -= new AvatarManager.AvatarPropertiesCallback(Avatars_OnAvatarProperties);
            client.Avatars.OnAvatarPicks -= new AvatarManager.AvatarPicksCallback(Avatars_OnAvatarPicks);
            client.Avatars.OnPickInfo -= new AvatarManager.PickInfoCallback(Avatars_OnPickInfo);
            netcom.ClientLoggedOut -= new EventHandler(netcom_ClientLoggedOut);
        }

        void Avatars_OnPickInfo(UUID pickid, ProfilePick pick)
        {
            System.Console.WriteLine("Pick" + pickid + ": " + pick.Name + "\n" + pick.Desc);
            System.Console.WriteLine(pick);
        }

        void Avatars_OnAvatarPicks(UUID id, Dictionary<UUID, string> picks)
        {
            System.Console.WriteLine("Picks for: " + id);
            foreach (KeyValuePair<UUID, string> pick in picks) {
                System.Console.WriteLine(pick.Value + ": " + pick.Key);
                client.Avatars.RequestPickInfo(id, pick.Key);
            }
        }

        private void netcom_ClientLoggedOut(object sender, EventArgs e)
        {
            Close();
        }

        private void Avatars_OnAvatarNames(Dictionary<UUID, string> names)
        {
            foreach (KeyValuePair<UUID, string> kvp in names)
            {
                BeginInvoke(new OnSetPartnerText(SetPartnerText), new object[] { kvp.Value });
                break;
            }
        }

        private delegate void OnSetPartnerText(string partner);
        private void SetPartnerText(string partner)
        {
            txtPartner.Text = partner;
        }

         //comes in on separate thread
        private void Avatars_OnAvatarProperties(UUID avatarID, Avatar.AvatarProperties properties)
        {
            if (avatarID != agentID) return;
            
            if (InvokeRequired) {
                Invoke(new MethodInvoker(delegate()
                {
                    Avatars_OnAvatarProperties(avatarID, properties);
                }));
                return;
            }

            FLImageID = properties.FirstLifeImage;
            SLImageID = properties.ProfileImage;
            
            if (SLImageID != UUID.Zero)
            {
                SLImageHandler pic = new SLImageHandler(instance, SLImageID, "");
                pic.Dock = DockStyle.Fill;
                slPicPanel.Controls.Add(pic);
                slPicPanel.Show();
            }
            else
            {
                slPicPanel.Hide();
            }

            if (FLImageID != UUID.Zero) {
                SLImageHandler pic = new SLImageHandler(instance, FLImageID, "");
                pic.Dock = DockStyle.Fill;
                rlPicPanel.Controls.Add(pic);
                rlPicPanel.Show();
            } else {
                rlPicPanel.Hide();
            }

            this.BeginInvoke(
                new OnSetProfileProperties(SetProfileProperties),
                new object[] { properties });
        }

        //called on GUI thread
        private delegate void OnSetProfileProperties(Avatar.AvatarProperties properties);
        private void SetProfileProperties(Avatar.AvatarProperties properties)
        {
            txtBornOn.Text = properties.BornOn;
            if (properties.Partner != UUID.Zero) client.Avatars.RequestAvatarName(properties.Partner);
            
            if (fullName.EndsWith("Linden")) rtbAccountInfo.AppendText("Linden Lab Employee\n");
            if (properties.Identified) rtbAccountInfo.AppendText("Identified\n");
            if (properties.Transacted) rtbAccountInfo.AppendText("Transacted\n");

            rtbAbout.AppendText(properties.AboutText);
            
            txtWebURL.Text = properties.ProfileURL;
            btnWebView.Enabled = btnWebOpen.Enabled = (txtWebURL.TextLength > 0);

            rtbAboutFL.AppendText(properties.FirstLifeText);
        }

        private void InitializeProfile()
        {
            txtFullName.Text = fullName;
            btnOfferTeleport.Enabled = btnPay.Enabled = (agentID != client.Self.AgentID);

            client.Avatars.RequestAvatarProperties(agentID);
            client.Avatars.RequestAvatarPicks(agentID);
        }

        private void btnClose_Click(object sender, EventArgs e)
        {
            Close();
        }

        private void btnWebView_Click(object sender, EventArgs e)
        {
            WebBrowser web = new WebBrowser();
            web.Dock = DockStyle.Fill;
            web.Url = new Uri(txtWebURL.Text);

            pnlWeb.Controls.Add(web);
        }

        private void ProcessWebURL(string url)
        {
            if (url.StartsWith("http://") || url.StartsWith("ftp://"))
                System.Diagnostics.Process.Start(url);
            else
                System.Diagnostics.Process.Start("http://" + url);
        }

        private void btnWebOpen_Click(object sender, EventArgs e)
        {
            ProcessWebURL(txtWebURL.Text);
        }

        private void rtbAbout_LinkClicked(object sender, LinkClickedEventArgs e)
        {
            ProcessWebURL(e.LinkText);
        }

        private void rtbAboutFL_LinkClicked(object sender, LinkClickedEventArgs e)
        {
            ProcessWebURL(e.LinkText);
        }

        private void btnOfferTeleport_Click(object sender, EventArgs e)
        {
            client.Self.SendTeleportLure(agentID, "Join me in " + client.Network.CurrentSim.Name + "!");
        }

        private void btnPay_Click(object sender, EventArgs e)
        {
            (new frmPay(instance, agentID, fullName, false)).ShowDialog();
        }

        private void textBox1_DragEnter(object sender, DragEventArgs e)
        {
            TreeNode node = e.Data.GetData(typeof(TreeNode)) as TreeNode;
            if (node == null)
            {
                e.Effect = DragDropEffects.None;
            }
            else
            {
                e.Effect = DragDropEffects.Copy;
            }
        }

        private void textBox1_DragDrop(object sender, DragEventArgs e)
        {
            TreeNode node = e.Data.GetData(typeof(TreeNode)) as TreeNode;
            if (node == null) return;

            if (node.Tag is InventoryItem)
            {
                InventoryItem item = node.Tag as InventoryItem;
                client.Inventory.GiveItem(item.UUID, item.Name, item.AssetType, agentID, true);
                instance.TabConsole.DisplayNotificationInChat("Offered item " + item.Name + " to " + fullName + ".");
            }
            else if (node.Tag is InventoryFolder)
            {
                InventoryFolder folder = node.Tag as InventoryFolder;
                client.Inventory.GiveFolder(folder.UUID, folder.Name, AssetType.Folder, agentID, true);
                instance.TabConsole.DisplayNotificationInChat("Offered folder " + folder.Name + " to " + fullName + ".");
            }
        }
    }
}