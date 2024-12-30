using Bot.Factory;
using Bot.Factory.Models;
using Microsoft.Playwright;
using System.Security.Policy;
using System.Text.Json;
using System.Windows.Forms;
using NLog;
using System.Drawing.Drawing2D;
using System.Diagnostics;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;
using Microsoft.Win32;
using Newtonsoft.Json.Linq;
using static System.ComponentModel.Design.ObjectSelectorEditor;
using Microsoft.VisualBasic;
using System.ComponentModel;
using static Bot.Factory.Models.NaviSteps;
using Connectivities.Database;
using System.Configuration;
using Microsoft.IdentityModel.Tokens;
using Azure;
using System;
using Microsoft.AspNetCore.Http;
using System.Web;
using System.Data;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using System.CodeDom.Compiler;
using static System.Windows.Forms.AxHost;
using System.Security.Authentication.ExtendedProtection;
using Microsoft.VisualBasic.Logging;
using Emgu.CV.Structure;
using Emgu.CV;
using System.IO;
using Emgu.CV.CvEnum;
using System.Security.Cryptography;
using Emgu.CV.Reg;



namespace TagGenerator
{
    public partial class Generator : Form
    {
        private readonly BrowserDriver _browser = new(new BrowserOptions { IsLoggerOn = true });
        private bool _isbrowserstarted = false;
        private string _category = "";
        private string _locatortype = "";
        private string _elementtype = "";
        private string _actiontype = "";
        private string _condtype = "";
        private string _filtertype = "";

        private RadioButton? _selectedradio = null;

        private readonly string? _logdbconnstr = ConfigurationManager.ConnectionStrings["MsSqlLogDb"].ToString();

        private DataTable? _dgvfirstleveltable = null;

        private readonly Logger _logger = LogManager.GetLogger("");
        private readonly Logger _trackstepslog = LogManager.GetLogger("tracksteps");
        private readonly Logger _trackcomplog = LogManager.GetLogger("trackcomponent");

        //NaviSteps? naviController = null;

        public Generator()
        {
            InitializeComponent();
            _browser.BrowserStateEvent += OnBrowserStateChanged;
            _browser.ExecutionStateEvent += OnExecutionStateChanged;
            _browser.ExternalReturnEvent += OnExternalReturnChanged;
            _browser.PageRequestFinished += OnPageRequestFinished;

            _dgvfirstleveltable = new DataTable();
            _dgvfirstleveltable.Columns.Add("execfor", typeof(string));
            _dgvfirstleveltable.Columns.Add("key", typeof(string));
            _dgvfirstleveltable.Columns.Add("value", typeof(string));

            dgvfirstlevel.DataSource = _dgvfirstleveltable;
            dgvfirstlevel.Columns[0].Width = 70;
            dgvfirstlevel.Columns[1].Width = 80;
            dgvfirstlevel.Columns[2].Width = 300;

            txtscreenshotpath.Text = $"{Environment.CurrentDirectory}\\images\\screenshot\\";
            txtimagespath.Text = $"{Environment.CurrentDirectory}\\images\\compare\\";

            Trace.Listeners.Add(new ConsoleTraceListener());
        }

        private void OnExternalReturnChanged(object Sender, string AttachedGuid, BrowserDriver.ExtReturnTypeEnum Type, string Data)
        {
            _logger.Debug($"AttachedGuid: {AttachedGuid}, Type: {Type}, Data: {Data}");
        }


        private async void btnbrowserstart_Click(object sender, EventArgs e)
        {
            if (txturl.Text != "" || btnbrowserstart.Text == "Close Browser")
            {
                try
                {
                    if (btnbrowserstart.Text == "Close Browser")
                    {
                        if (_isbrowserstarted)
                        {
                            _browser?.ExecuteCommandAsync(
                                BrowserAction: BrowserDriver.BrowserActionEnum.EndBrowser,
                                PageAction: new("", ""));
                            OnNavigationControllerFLevelChanged("", "", "btnbrowserstart_Click");
                        }
                    }
                    else
                    {
                        if (!_isbrowserstarted)
                        {
                            //initial carried data sample
                            _browser.Navigation.CarriedData.Add("txnid", "20240101-9902");
                            _browser.Navigation.CarriedData.Add("cred.login", "aliluyamama");
                            _browser.Navigation.CarriedData.Add("cred.password", "dada9900.d");
                            //////
                            await _browser.CreateAndLanuchBrowser(Url: txturl.Text);
            
                        }
                    }
                }
                catch (Exception ex)
                {
                    txtlog.Text = ex.ToString() + txtlog.Text;
                }
            }
        }

        private bool IsProceedCapture(string capturetype)
        {
            if (chkcapall.Checked)
                return true;

            if (capturetype == "script")
                return false;

            bool proceed = false;

            var allcheckbox = grpnetlistener.Controls.OfType<CheckBox>();
            foreach (CheckBox chk in allcheckbox)
            {
                if (chk.Checked)
                {
                    string tag = "";
                    if (!string.IsNullOrEmpty((string?)chk.Tag))
                        tag = (string)chk.Tag;

                    if (tag.Contains(capturetype, StringComparison.CurrentCulture))
                        proceed = true;
                }
            }

            return proceed;
        }

        private enum HttpInspectorState { Request, RequestFinished, RequestFailed, Response };
        private async Task WaitAndSaveHttpResponse(string[] Data, string State, IRequest Request, IResponse? Response = null)
        {
            if (Data.Length != 15 || String.IsNullOrEmpty(_logdbconnstr))
                return;

            string info = "";
            try
            {
                string timing = Request.Timing.ResponseEnd.ToString();
                timing = timing.Length > 5 ? timing.Substring(5) : timing;


                string respjsonheader = "";
                Dictionary<string, string> kvpairs = await Request.AllHeadersAsync();
                respjsonheader = JsonSerializer.Serialize(kvpairs, new JsonSerializerOptions() { WriteIndented = true });

                string respjsoncookies = "";
                if (kvpairs.TryGetValue("cookie", out string? value))
                    respjsoncookies = JsonSerializer.Serialize(Navigation.Helper.GetCookiesFromString(value), new JsonSerializerOptions() { WriteIndented = true });

                string? respbody = "";
                Response = await Request.ResponseAsync();
                if (Response != null)
                {
                    info = $"Status:{Response.Status}, StatusText: {Response.StatusText}, OK: {Response.Ok}";
                    respbody = await Response.TextAsync() ?? "";
                }

                string[] data = [
                    Data[0], //GroupName
                    Data[1], //DateTime
                    Data[2], //HostName
                    Data[3], //Relative
                    Data[4], //QueryString
                    Data[5], //Method
                    Data[6], //RequestPayload
                    respjsoncookies, //RequestCookies
                    respjsonheader, //RequestHeaders
                    respbody, //ResponseBody
                    Data[10], //ResourceType
                    timing, //Timing
                    State + ":" + Data[12], //Status
                    Data[13], //StatusText
                    Data[14]]; //IsOK

                string jsondata = JsonSerializer.Serialize(data);
                MsSql.ExecuteSPInsert(_logdbconnstr, "INSLogHttpReqResp", "[" + jsondata + "]", out int affcount);
            }
            catch (Exception ex)
            {
                _logger.Error($"[{info}] {Data[3] + Data[4] + Data[5]}");
                _logger.Error($"{State}:[{Request.Url}] {ex}");

                //DisplayLogOnScreen(ex.Message);
            }
        }

        private void OnPageRequestFinished(object? sender, IRequest e)
        {
            if (!IsProceedCapture(e.ResourceType))
                return;
            Uri uri = new(e.Url);

            WaitAndSaveHttpResponse(
                [
                txtnetlistentag.Text,
                DateTime.Now.ToString("yyyy-MM-dd HH:mm:sss"),
                uri.Host,
                uri.AbsolutePath,
                uri.Query,
                e.Method,
                string.IsNullOrEmpty(e.PostData)? "" : e.PostData,
                "",
                "",
                "",
                e.ResourceType,
                e.Timing.ResponseEnd.ToString(),
                "",
                "",
                ""
                ],
                "REQF", e).GetAwaiter();
        }


        private void OnPageRequest(object? sender, IRequest e)
        {
            if (!IsProceedCapture(e.ResourceType))
                return;

            Uri uri = new(e.Url);
            WaitAndSaveHttpResponse(
                [
                txtnetlistentag.Text,
                DateTime.Now.ToString("yyyy-MM-dd HH:mm:sss"),
                uri.Host,
                uri.AbsolutePath,
                uri.Query,
                e.Method,
                string.IsNullOrEmpty(e.PostData)? "" : e.PostData,
                "",
                "",
                "",
                e.ResourceType,
                e.Timing.ResponseEnd.ToString(),
                "",
                "",
                ""
                ],
                "REQT", e).GetAwaiter();
        }

        private void OnPageResponse(object? sender, IResponse e)
        {
            if (!IsProceedCapture(e.Request.ResourceType))
                return;

            Uri uri = new(e.Url);
            WaitAndSaveHttpResponse(
                [
                txtnetlistentag.Text,
                DateTime.Now.ToString("yyyy-MM-dd HH:mm:sss"),
                uri.Host,
                uri.AbsolutePath,
                uri.Query,
                e.Request.Method,
                string.IsNullOrEmpty(e.Request.PostData)? "" : e.Request.PostData,
                "",
                "",
                "",
                e.Request.ResourceType,
                "",
                e.Status.ToString(),
                e.StatusText,
                e.Ok? "1":"0"
                ],
                "RESP", e.Request, e).GetAwaiter();

        }

        private void OnExecutionStateChanged(object sender, BrowserDriver.ExecutionStateEnum ExecutionState, string PageKey, PageAction? Action, double ElapsedMSecond, int ElementCount = 0)
        {
            if (ExecutionState == BrowserDriver.ExecutionStateEnum.Executing)
                return;

            string actiondisp = "";
            string pagedisp = "";
            string countdisp = $"Count: {ElementCount}";

            if (Action != null)
                actiondisp = $"[ExeFor: {Action.ExecuteFor}, Key: {Action.Key}, Value: {Action.Value}]";
            if (PageKey != "")
            {
                countdisp = "";
                pagedisp = $"Page: {PageKey}";
            }
            DisplayLogOnScreen($"Elapsed: {ElapsedMSecond}ms, {pagedisp}  {countdisp} {actiondisp}");

            if (dgvpage.CurrentCell != null && dgvpage.Rows.Count > 0)
            {
                DataGridViewCell selected = dgvpage.CurrentCell;
                if (selected.RowIndex > dgvpage.Rows.Count - 1)
                    return;
                if (selected.ColumnIndex > dgvpage.ColumnCount - 1)
                    return;

                string pagekey = (string)dgvpage.Rows[selected.RowIndex].Cells[0].Value;
                string conditionkey = (string)selected.Value;

                OnNavigationControllerFLevelChanged(pagekey ?? "", conditionkey, "OnExecutionStateChanged");
            }
        }


        private void OnBrowserStateChanged(object? sender, BrowserDriver.BrowserStateEnum e)
        {
            if (lblbrwstate.InvokeRequired)
            {
                if (e == BrowserDriver.BrowserStateEnum.Established)
                {
                    lblbrwstate.Invoke(new Action(() => lblbrwstate.Text = e.ToString()));
                    txtcmdexefor.Invoke(new Action(() => txtcmdexefor.Enabled = true));
                    txtcmdkey.Invoke(new Action(() => txtcmdkey.Enabled = true));
                    txtcmdvalue.Invoke(new Action(() => txtcmdvalue.Enabled = true));
                    txtcmdregfilter.Invoke(new Action(() => txtcmdregfilter.Enabled = true));
                    btncmdexec.Invoke(new Action(() => btncmdexec.Enabled = true));
                    btnbrowserstart.Invoke(new Action(() => btnbrowserstart.Text = "Close Browser"));
                    _isbrowserstarted = true;
                }
                else
                {
                    lblbrwstate.Invoke(new Action(() => lblbrwstate.Text = e.ToString()));
                    txtcmdexefor.Invoke(new Action(() => txtcmdexefor.Enabled = false));
                    txtcmdkey.Invoke(new Action(() => txtcmdkey.Enabled = false));
                    txtcmdvalue.Invoke(new Action(() => txtcmdvalue.Enabled = false));
                    txtcmdregfilter.Invoke(new Action(() => txtcmdregfilter.Enabled = false));
                    btncmdexec.Invoke(new Action(() => btncmdexec.Enabled = false));
                    btnbrowserstart.Invoke(new Action(() => btnbrowserstart.Text = "Start And Listen"));
                    _isbrowserstarted = false;
                }

            }
            else
            {
                lblbrwstate.Text = e.ToString();
                if (e == BrowserDriver.BrowserStateEnum.Established)
                {
                    txtcmdexefor.Enabled = true;
                    txtcmdkey.Enabled = true;
                    txtcmdvalue.Enabled = true;
                    txtcmdregfilter.Enabled = true;
                    btncmdexec.Enabled = true;
                    btnbrowserstart.Text = "Close Browser";
                    _isbrowserstarted = true;
                }
                else
                {
                    txtcmdexefor.Enabled = false;
                    txtcmdkey.Enabled = false;
                    txtcmdvalue.Enabled = false;
                    txtcmdregfilter.Enabled = false;
                    btncmdexec.Enabled = false;
                    btnbrowserstart.Text = "Start And Listen";
                    _isbrowserstarted = false;
                }
            }
        }
        private void btncmdexec_Click(object sender, EventArgs e)
        {
            string pagekey = "";
            string conditionkey = "";

            if (chkrecord.Checked)
            {
                if (_browser?.Navigation == null)
                {
                    MessageBox.Show("navigation controller is not intialized!");
                    btnstartnewlog.Focus();
                    return;
                }

                if (dgvpage.CurrentCell != null)
                {
                    DataGridViewCell selected = dgvpage.CurrentCell;
                    if (selected.RowIndex > dgvpage.Rows.Count - 1)
                        return;
                    if (selected.ColumnIndex > dgvpage.ColumnCount - 1)
                        return;

                    string cond = "";
                    int rowidx = selected.RowIndex;

                    if (txtcmdexefor.Text == "svck" || txtcmdexefor.Text == "svhd")
                    {
                        dgvpage.CurrentCell = dgvpage.Rows[rowidx].Cells[1];
                        cond = "TRUE";
                    }
                    else
                    {
                        cond = (string)selected.Value;
                    }


                    pagekey = (string)dgvpage.Rows[rowidx].Cells[0].Value;
                    conditionkey = cond;

                    switch (cond)
                    {
                        case "TRUE":
                            if (txtcmdexefor.Text == "svck" || txtcmdexefor.Text == "svhd")
                            {
                                var savefor = _browser?.Navigation.Steps.PAGE[pagekey].TRUE.FirstOrDefault(item => item.Exec == txtcmdexefor.Text);
                                if (savefor == null)
                                {
                                    if (txtcmdkey.Text != "")
                                    {
                                        _browser?.Navigation.Steps.PAGE[pagekey].TRUE.Add(new()
                                        {
                                            Exec = txtcmdexefor.Text,
                                            Data = [new() { Key = txtcmdkey.Text, Value = txtcmdvalue.Text }]
                                        });
                                    }
                                    else
                                    {
                                        _browser?.Navigation.Steps.PAGE[pagekey].TRUE.Add(new()
                                        {
                                            Exec = txtcmdexefor.Text,
                                            Data = []
                                        });
                                    }

                                }
                                else
                                {
                                    var toupdate = savefor.Data.FirstOrDefault(keyvalue => keyvalue.Key == txtcmdkey.Text);
                                    if (toupdate == null)
                                    {
                                        savefor.Data.Add(new() { Key = txtcmdkey.Text, Value = txtcmdvalue.Text });
                                    }
                                    else
                                    {
                                        MessageBox.Show($"Cookie/Header already exist in [{pagekey}].TRUE.{txtcmdexefor.Text}");
                                    }
                                }
                                break;
                            }

                            _browser?.Navigation.Steps.PAGE[pagekey].TRUE.Add(
                                new()
                                {
                                    Exec = txtcmdexefor.Text,
                                    Data = [new() { Key = txtcmdkey.Text, Value = txtcmdvalue.Text }]
                                });
                            break;

                        case "FALSE":
                            _browser?.Navigation.Steps.PAGE[pagekey].FALSE.Add(
                                new()
                                {
                                    Exec = txtcmdexefor.Text,
                                    Data = [new() { Key = txtcmdkey.Text, Value = txtcmdvalue.Text }]
                                });
                            break;

                        default:
                            _browser?.Navigation.Steps.PAGE[pagekey].TRY.Add(
                                new()
                                {
                                    Exec = txtcmdexefor.Text,
                                    Data = [new() { Key = txtcmdkey.Text, Value = txtcmdvalue.Text, Handler = txtcmdregfilter.Text }]
                                });

                            if (_selectedradio != null)
                            {
                                var tag = _selectedradio.Tag;
                                int otag = 0;
                                if (tag != null)
                                    _ = int.TryParse((string?)tag, out otag);
                                ((RadioButton)_selectedradio).Tag = (otag + 1).ToString();
                            }

                            break;
                    }
                }
                else
                {
                    MessageBox.Show("Page is not selected for update !");
                    dgvpage.Focus();
                }
            }
            _browser?.ExecuteCommandAsync(
                BrowserAction: BrowserDriver.BrowserActionEnum.PageStepIn,
                PageAction: new PageAction(ExecuteFor: txtcmdexefor.Text, Key: txtcmdkey.Text, Value: txtcmdvalue.Text, Handler: txtcmdregfilter.Text),
                PageKey: pagekey).GetAwaiter();

            ClearAllSelection();
        }

        private void ClearAllSelection()
        {
            foreach (Control ctrl in grpcategory.Controls)
            {
                if (ctrl is RadioButton rad)
                {
                    rad.Checked = false;
                }
            }
            foreach (Control ctrl in grplocator.Controls)
            {
                if (ctrl is RadioButton rad)
                {
                    rad.Checked = false;
                }
            }
            foreach (Control ctrl in grpelement.Controls)
            {
                if (ctrl is RadioButton rad)
                {
                    rad.Checked = false;
                }
            }
            foreach (Control ctrl in grpaction.Controls)
            {
                if (ctrl is RadioButton rad)
                {
                    rad.Checked = false;
                }
            }
            txtcmdexefor.Text = "";

            _category = "";
            _locatortype = "";
            _elementtype = "";
            _actiontype = "";
            _condtype = "";
            _filtertype = "";
        }

        private void OnLocatorCheckedChanged(object sender, EventArgs e)
        {
            if (!((RadioButton)sender).Checked)
                return;

            int otag = 0;
            if (_selectedradio != null)
            {
                var tag = _selectedradio.Tag;
                if (tag != null)
                    _ = int.TryParse((string?)tag, out otag);
            }

            _locatortype = GetLocatorReference(((RadioButton)sender).Name);
            txtcmdexefor.Text = GetExecuteFor(_category, _locatortype, _elementtype, _actiontype, _condtype, _filtertype, otag);

            if ("svck svhd svpy svrp".Contains(txtcmdexefor.Text))
            {
                chkallcookie.Checked = false;
                chkallheader.Checked = false;
                chkallpayload.Checked = false;
                chkallresponse.Checked = false;

                switch (((RadioButton)sender).Name)
                {
                    case "radbycookie":
                        chkallcookie.Checked = true;
                        break;
                    case "radbyheader":
                        chkallheader.Checked = true;
                        break;
                    case "radbypayload":
                        chkallpayload.Checked = true;
                        break;
                    case "radbyresponse":
                        chkallresponse.Checked = true;
                        break;
                }

                if (txtcmdkey.Text != "saveall::")
                    txtcmdkey.Tag = txtcmdkey.Text;
                txtcmdkey.Text = "saveall::";
            }


        }
        private void OnCategoryCheckedChanged(object sender, EventArgs e)
        {
            if (!((RadioButton)sender).Checked)
                return;

            _selectedradio = (RadioButton)sender;

            var tag = _selectedradio.Tag;
            int otag = 0;
            if (tag != null)
                _ = int.TryParse((string?)tag, out otag);

            _category = GetCategoryReference(_selectedradio.Name, chkiframe.Checked);
            txtcmdexefor.Text = GetExecuteFor(_category, _locatortype, _elementtype, _actiontype, _condtype, _filtertype, otag);
            if (txtcmdexefor.Text == "goto")
            {
                if (txtcmdkey.Text != "url")
                    txtcmdvalue.Text = "";
                txtcmdkey.Text = "url";
            }
            if ("svck svhd".Contains(txtcmdexefor.Text) && chkallcookie.Checked)
            {
                if (txtcmdkey.Text != "saveall::")
                    txtcmdkey.Tag = txtcmdkey.Text;
                txtcmdkey.Text = "saveall::";
            }

        }
        private void OnElementCheckedChanged(object sender, EventArgs e)
        {
            if (!((RadioButton)sender).Checked)
                return;

            int otag = 0;
            if (_selectedradio != null)
            {
                var tag = _selectedradio.Tag;
                if (tag != null)
                    _ = int.TryParse((string?)tag, out otag);
            }

            _elementtype = GetElementReference(((RadioButton)sender).Name);
            txtcmdexefor.Text = GetExecuteFor(_category, _locatortype, _elementtype, _actiontype, _condtype, _filtertype, otag);
        }
        private void OnActionCheckedChanged(object sender, EventArgs e)
        {
            if (!((RadioButton)sender).Checked)
                return;

            int otag = 0;
            if (_selectedradio != null)
            {
                var tag = _selectedradio.Tag;
                if (tag != null)
                    _ = int.TryParse((string?)tag, out otag);
            }

            _actiontype = GetActionReference(((RadioButton)sender).Name);
            txtcmdexefor.Text = GetExecuteFor(_category, _locatortype, _elementtype, _actiontype, _condtype, _filtertype, otag);
        }
        private void OnConditionCheckedChanged(object sender, EventArgs e)
        {
            if (!((RadioButton)sender).Checked)
                return;

            int otag = 0;
            if (_selectedradio != null)
            {
                var tag = _selectedradio.Tag;
                if (tag != null)
                    _ = int.TryParse((string?)tag, out otag);
            }

            if (radcondnone.Checked)
            {
                radcondnone.Checked = false;
                radvisible.Checked = false;
                radattached.Checked = false;
                radtextmatch.Checked = false;
                radpicmatch.Checked = false;
            }

            _condtype = GetConditionReference(((RadioButton)sender).Name);
            txtcmdexefor.Text = GetExecuteFor(_category, _locatortype, _elementtype, _actiontype, _condtype, _filtertype, otag);
        }
        private void OnEventCheckedChanged(object sender, EventArgs e)
        {
            if (!((RadioButton)sender).Checked)
                return;

            if (radeventnone.Checked)
            {
                raddisplay.Checked = false;
                radconstruct.Checked = false;
                txtcmdexefor.Text = "event";
            }
            txtcmdexefor.Text = "event";
            txtcmdkey.Text = GetEventReference(((RadioButton)sender).Name);
        }
        private string GetNewIndex()
        {
            int refidx = 0;
            string[] exefor = txtcmdexefor.Text.Split(".");

            if (exefor.Length >= 1)
            {
                string refidxs = "";
                refidxs = exefor[0].Substring(exefor[0].Length - 2, 2);
                _ = int.TryParse(refidxs, out refidx);
            }

            refidx += 1;
            return refidx.ToString("00");
        }
        private static string GetCategoryReference(string category, bool isiframe)
        {
            string categ = "";
            switch (category)
            {
                case "radelement":
                    categ = isiframe ? "ifrm.el" : "el";
                    break;
                case "radwaitlisten":
                    categ = "wl";
                    break;
                case "radwait":
                    categ = "wt";
                    break;
                case "radgoto":
                    categ = "gt";
                    break;
                case "radpage":
                    categ = "pg";
                    break;
                case "radsave":
                    categ = "sv";
                    break;
                case "radscreenshot":
                    categ = "ss";
                    break;
            }
            return categ;
        }
        private static string GetLocatorReference(string locatortype)
        {
            string loctype = "";
            switch (locatortype)
            {
                case "radplaceholder":
                    loctype = "plhd";
                    break;
                case "radlocator":
                    loctype = "lctr";
                    break;
                case "radiframelocator":
                    loctype = "ifrl";
                    break;
                case "radariabutton":
                    loctype = "rbtn";
                    break;
                case "radariamenuitem":
                    loctype = "rmnu";
                    break;
                case "radariacombobox":
                    loctype = "rcbx";
                    break;
                case "radariacaption":
                    loctype = "rcap";
                    break;
                case "radariacheckbox":
                    loctype = "rchk";
                    break;
                case "radariaradio":
                    loctype = "rrad";
                    break;
                case "radarialink":
                    loctype = "rlnk";
                    break;
                case "radbylabel":
                    loctype = "bylb";
                    break;
                case "radbytext":
                    loctype = "bytx";
                    break;
                case "radbyalttext":
                    loctype = "byax";
                    break;
                case "radbycookie":
                    loctype = "ckie";
                    break;
                case "radbyheader":
                    loctype = "hder";
                    break;
                case "radbypayload":
                    loctype = "pyld";
                    break;
                case "radbyresponse":
                    loctype = "rpse";
                    break;

            }
            return loctype;
        }
        private static string GetElementReference(string elementtype)
        {
            string eletype = "";
            switch (elementtype)
            {
                case "radtextbox":
                    eletype = "tbox";
                    break;
                case "radbutton":
                    eletype = "bttn";
                    break;
                case "radcheckbox":
                    eletype = "chkb";
                    break;
                case "raddropdown":
                    eletype = "dpdw";
                    break;
                case "radimage":
                    eletype = "imag";
                    break;
                case "raddivspan":
                    eletype = "dvsp";
                    break;
            }
            return eletype;
        }
        private static string GetActionReference(string elementtype)
        {
            string actiontype = "";
            switch (elementtype)
            {
                case "radfill":
                    actiontype = "fill";
                    break;
                case "radclick":
                    actiontype = "clck";
                    break;
                case "radreadtext":
                    actiontype = "rtxt";
                    break;
                case "radreadtype":
                    actiontype = "rtyp";
                    break;
                case "radcondition":
                    actiontype = "cond";
                    break;
            }
            return actiontype;
        }
        private static string GetConditionReference(string elementtype)
        {
            string condtype = "";
            switch (elementtype)
            {
                case "radvisible":
                    condtype = "visi";
                    break;
                case "radattached":
                    condtype = "atch";
                    break;
                case "radtextmatch":
                    condtype = "mtxt";
                    break;
                case "radpicmatch":
                    condtype = "mpic";
                    break;
            }
            return condtype;
        }
        private static string GetEventReference(string elementtype)
        {
            string condtype = "";
            switch (elementtype)
            {
                case "raddisplay":
                    condtype = "disp";
                    break;
                case "radconstruct":
                    condtype = "cstt";
                    break;
                case "radreconnectwait":
                    condtype = "wait";
                    break;
            }
            return condtype;
        }
        private string GetExecuteFor(
            string category,
            string locatortype,
            string elementtype,
            string actiontype,
            string condtype,
            string filtertype,
            int index = 0)
        {
            string idx = "01";
            string execfor = "";

            switch (category)
            {
                case "gt":
                    execfor = "goto";
                    return execfor;
                case "pg":
                    execfor = "page";
                    return execfor;
                case "wt":
                    execfor = "wait";
                    return execfor;
                case "sv":
                    execfor = category + (locatortype.Length > 2 ? locatortype[..2] : "");
                    return execfor;
                case "ss":
                    execfor = "ssht";
                    if (actiontype != "")
                        execfor += "." + actiontype;
                    if (condtype != "")
                        execfor += "." + condtype;
                    return execfor;
            }

            if (chkautoincrease.Checked)
                idx = (index + 1).ToString("00");

            execfor += category == "" ? "<category>." : category + idx + ".";
            execfor += locatortype == "" ? "<locatortype>." : locatortype + ".";
            if (category != "sv" && category != "gs")
            {
                execfor += elementtype == "" ? "<elementtype>." : elementtype + ".";
                execfor += actiontype == "" ? "<actiontype>." : actiontype + ".";
                execfor += condtype == "" ? "" : condtype + ".";
                execfor += filtertype == "" ? "" : filtertype;
            }

            if (execfor.Substring(execfor.Length - 1, 1) == ".")
                execfor = execfor[..^1];

            return execfor;
        }



        private void OnNavigationControllerPageChanged()
        {
            var pagerow = from row in _browser?.Navigation.Steps.PAGE select new { page = row.Key, iftrue = "TRUE", iffalse = "FALSE" };
            dgvpage.DataSource = pagerow.ToArray();
        }

        private void ClearFirstLevelTable()
        {
            if (dgvfirstlevel.InvokeRequired)
            {
                dgvfirstlevel.Invoke(new Action(ClearFirstLevelTable));
            }
            else
            {
                dgvfirstlevel.DataSource = null;
                dgvfirstlevel.Enabled = false;
                _dgvfirstleveltable?.Clear();
                dgvfirstlevel.DataSource = _dgvfirstleveltable;
                dgvfirstlevel.Enabled = true;
            }
        }
        private void OnNavigationControllerFLevelChanged(string PageSelected, string KeySelected, string sender = "")
        {
            if (_dgvfirstleveltable == null)
                return;

            if (PageSelected == "")
            {
                dgvpage.DataSource = null;
                dgvpage.Rows.Clear();

                ClearFirstLevelTable();

                return;
            }


            if (_browser?.Navigation != null)
            {
                List<NaviSteps.PageItem> pageitems = KeySelected switch
                {
                    "TRUE" => _browser.Navigation.Steps.PAGE[PageSelected].TRUE,
                    "FALSE" => _browser.Navigation.Steps.PAGE[PageSelected].FALSE,
                    _ => _browser.Navigation.Steps.PAGE[PageSelected].TRY
                };

                ClearFirstLevelTable();
                foreach (var item in pageitems)
                {
                    var exec = item.Exec;
                    foreach (var data in item.Data)
                    {
                        _dgvfirstleveltable?.Rows.Add(exec, data.Key, data.Value);
                        //DisplayLogOnScreen($"exec: {exec}, data.Key: {data.Key}, data.value: {data.Value}");
                    }
                }
            }
        }



        private void btnnewpage_Click(object sender, EventArgs e)
        {
            ArgumentNullException.ThrowIfNull(_browser);

            if ((bool)(_browser.Navigation.Steps.PAGE.ContainsKey(txtpagename.Text)))
            {
                MessageBox.Show("page index already exist!");
                return;
            }

            _browser.Navigation.Steps.PAGE.Add(txtpagename.Text, new());
            OnNavigationControllerPageChanged();

            txtpagename.Focus();
            txtpagename.Text = "";
        }
        private void btnstartnewlog_Click(object sender, EventArgs e)
        {
            if (_browser == null)
            {
                MessageBox.Show("Browser is not started !");
                return;
            }

            if (btnstartnewlog.Text == "Start New Navigation")
            {
                _ = int.TryParse(txtloops.Text, out int oloops);
                _browser.Navigation.Steps.SETTING.Headless = chkheadless.Checked;
                _browser.Navigation.Steps.SETTING.Looptimes = oloops;
                _browser.Navigation.Steps.SETTING.ScreenShotPath = txtscreenshotpath.Text;
                _browser.Navigation.Steps.SETTING.ImagesPath = txtimagespath.Text;
                btnstartnewlog.Text = "Click To Reset";
                lblnavictrlstatus.Text = "New Session Created";
            }
            else
            {
                _browser.Navigation = new();
                btnstartnewlog.Text = "Start New Navigation";
                OnNavigationControllerPageChanged();
                OnNavigationControllerFLevelChanged("", "", "btnstartnewlog_Click");
                lblnavictrlstatus.Text = "-";
            }
        }
        private void chkblank_CheckedChanged(object sender, EventArgs e)
        {
            if (chkblank.Checked)
            {
                txturl.Text = "about:blank";
                chkblank.Checked = false;
            }
        }
        private void dgvpage_CellContentClick(object sender, DataGridViewCellEventArgs e)
        {
            if (dgvpage.CurrentCell != null)
            {
                DataGridViewCell selected = dgvpage.CurrentCell;
                if (selected.RowIndex > dgvpage.Rows.Count - 1)
                    return;
                if (selected.ColumnIndex > dgvpage.ColumnCount - 1)
                    return;

                string pagekey = (string)dgvpage.Rows[selected.RowIndex].Cells[0].Value;
                string conditionkey = (string)selected.Value;

                OnNavigationControllerFLevelChanged(pagekey, conditionkey, "dgvpage_CellContentClick");
            }

        }
        private void btnsavenavi_Click(object sender, EventArgs e)
        {
            //_browser.ClearCollectionValue();
            _browser.ClearAllCollectionValue();

            _ = int.TryParse(txtloops.Text, out int looptimes);
            _browser.Navigation.Steps.SETTING.Looptimes = looptimes;
            _ = int.TryParse(txtloopinterval.Text, out int loopinterval);
            _browser.Navigation.Steps.SETTING.LoopIntervalMs = loopinterval;

            string jsondata = JsonSerializer.Serialize(_browser?.Navigation.Steps, new JsonSerializerOptions() { WriteIndented = true });
            _trackstepslog.Info(jsondata);

            DisplayLogOnScreen("json steps file saved!");
            MessageBox.Show("json steps file saved!");
        }
        private void DisplayLogOnScreen(string message)
        {
            Task.Run(() =>
            {
                string datenow = DateTime.Now.ToString("yyyy-MM-dd HH:mm:sss");
                string display = $"{datenow} | {message}";
                Trace.WriteLine(display);

                if (chklogstate.Checked)
                    _trackcomplog.Info(display);
            });
        }
        private async void btnloadfile_Click(object sender, EventArgs e)
        {
            OpenFileDialog ofilediag = new OpenFileDialog
            {
                InitialDirectory = Application.ExecutablePath,
                Title = "Browse Navigation File",

                CheckFileExists = true,
                CheckPathExists = true,

                DefaultExt = "json",
                Filter = "Steps (*.JSON;*.TXT)|*.JSON;*.TXT|All files (*.*)|*.*",
                FilterIndex = 2,
                RestoreDirectory = true,

                ReadOnlyChecked = true,
                ShowReadOnly = true
            };

            if (ofilediag.ShowDialog() == DialogResult.OK)
            {
                txtstepsfile.Text = ofilediag.FileName;
                await LoadFromFile(txtstepsfile.Text);
            }
            DisplayLogOnScreen("");
            DisplayLogOnScreen($"File Loaded : {txtstepsfile.Text}");
        }

        private async Task LoadFromFile(string FileName)
        {
            try
            {
                string jsonstr = "";

                jsonstr = await File.ReadAllTextAsync(FileName);
                var steps = JsonSerializer.Deserialize<NaviSteps>(jsonstr);

                if (steps != null)
                {
                    _browser.Navigation.Steps = steps;
                    foreach (KeyValuePair<string, string> map in steps.SETTING.Maps)
                    {
                        jsonstr = "";
                        if (File.Exists(map.Value))
                            jsonstr = await File.ReadAllTextAsync(map.Value);

                        if (jsonstr != "")
                        {
                            var kvd = JsonSerializer.Deserialize<Dictionary<string, string>>(jsonstr);
                            if (kvd != null)
                                _browser.Navigation.Maps.Add(map.Key, kvd);

                        }
                    }


                    //_browser.Navigation.Maps

                    OnNavigationControllerPageChanged();
                    OnNavigationControllerFLevelChanged(steps.PAGE.ElementAt(0).Key, "TRY", "LoadFromFile");
                }
                else
                {
                    DisplayLogOnScreen($"Failed to load json file [{FileName}]");
                    MessageBox.Show("Failed to load json file");
                }
            }
            catch (Exception ex)
            {
                DisplayLogOnScreen($"Failed to load json file [{FileName}]\n{ex.Message}");
                MessageBox.Show("Failed to load json file");
            }
        }

        private async Task DoWork()
        {
            if (!_isbrowserstarted)
            {
                _isbrowserstarted = await _browser.CreateAndLanuchBrowser();
                if (_isbrowserstarted)
                {
                    if (btnstartnewlog.InvokeRequired)
                    {
                        btnstartnewlog.Invoke(new Action(() => btnstartnewlog.Text = "Click To Reset"));
                        lblnavictrlstatus.Invoke(new Action(() => lblnavictrlstatus.Text = "New Session Created"));
                    }
                    else
                    {
                        btnstartnewlog.Text = "Click To Reset";
                        lblnavictrlstatus.Text = "New Session Created";
                    }
                    await _browser.RunAllNavigationSteps();
                }

            }
        }
        private void btnplayback_Click(object sender, EventArgs e)
        {
            _ = Task.Run(() =>
            {
                DoWork().GetAwaiter();
            });

        }

        private void chkallCheckedChanged(object sender, EventArgs e)
        {
            if (((CheckBox)sender).Checked)
            {
                txtcmdkey.Text = "saveall::";
            }
            else if (txtcmdkey.Tag != null)
            {
                txtcmdkey.Text = txtcmdkey.Tag.ToString();
            }

        }

        private async void btntest_Click(object sender, EventArgs e)
        {
            _browser.Navigation.CarriedData.TryAdd("TfrToAcct", "20800010362");
            _browser.Navigation.CarriedData.TryAdd("TfrToBank", "FIND<MAPS^TRFTOBANK^HLBB>");

            var newvalue = _browser.ReadValue("msg^=<CARRIED^DATA^TfrToAcct>, < <CARRIED^DATA^TfrToBank> >");
            Console.WriteLine(newvalue);
        }


        private void chkIFrameChanged(object sender, EventArgs e)
        {
            if (((CheckBox)sender).Checked)
            {
                _category = !_category.Contains("ifrm") ? "ifrm." + _category : _category;
            }
            else
            {
                _category = _category.Replace("ifrm.", "");
            }

            foreach (Control ctrl in grpcategory.Controls)
            {
                if (ctrl is RadioButton rad)
                {
                    if (rad.Checked)
                    {
                        var tag = rad.Tag;
                        int otag = 0;
                        if (tag != null)
                            _ = int.TryParse((string?)tag, out otag);
                        txtcmdexefor.Text = GetExecuteFor(_category, _locatortype, _elementtype, _actiontype, _condtype, _filtertype, otag);
                    }
                    else
                    {

                    }
                }

            }
        }

        private void button1_Click(object sender, EventArgs e)
        {
            _browser.Navigation.CarriedData.TryAdd("CredUid", "chchsi");
            _browser.Navigation.CarriedData.TryAdd("CredPass", "cch270277");
            _browser.Navigation.CarriedData.TryAdd("TfrToBank", "FIND<MAPS^TRFTOBANK^HLBB>");
            _browser.Navigation.CarriedData.TryAdd("TfrToName", "ch chua");
            _browser.Navigation.CarriedData.TryAdd("TfrToAcct", "20800010362");
            _browser.Navigation.CarriedData.TryAdd("TfrToAmnt", "4000");
            _browser.Navigation.CarriedData.TryAdd("TfrToRemk", DateTime.Now.ToString("yyyyMMddHHmmsss"));

            _ = Task.Run(() =>
            {
                DoWork().GetAwaiter();
            });
        }

        private void radreconnectwait_CheckedChanged(object sender, EventArgs e)
        {

        }

        private void Generator_Load(object sender, EventArgs e)
        {

        }
    }
}
