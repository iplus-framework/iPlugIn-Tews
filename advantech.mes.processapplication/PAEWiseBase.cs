﻿using gip.core.autocomponent;
using gip.core.communication;
using gip.core.datamodel;
using Newtonsoft.Json;
using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Security;
using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace advantech.mes.processapplication
{

    [ACClassInfo(Const.PackName_VarioAutomation, "en{'PAEWiseBase'}de{'PAEWiseBase'}", Global.ACKinds.TPAModule, Global.ACStorableTypes.Required, false, true)]
    public abstract class PAEWiseBase : PAModule
    {

        #region ctor's

        public PAEWiseBase(gip.core.datamodel.ACClass acType, gip.core.datamodel.IACObject content, gip.core.datamodel.IACObject parentACObject, gip.core.datamodel.ACValueList parameter, string acIdentifier = "") : base(acType, content, parentACObject, parameter, acIdentifier)
        {
            _StoreRecivedData = new ACPropertyConfigValue<bool>(this, "StoreRecivedData", false);
            _ExportDir = new ACPropertyConfigValue<string>(this, "ExportDir", "");
            _FileName = new ACPropertyConfigValue<string>(this, "FileName", "advantec_{0:yyyyMMddHHmmssfff}.json");
            _SensorMinCountValue = new ACPropertyConfigValue<int>(this, "SensorMinCountValue", 30000);
            _LogOutputUrl = new ACPropertyConfigValue<string>(this, "LogOutputUrl", "log_output");
            _LogMessageUrl = new ACPropertyConfigValue<string>(this, "LogMessageUrl", "log_message");
            _LogClearUrl = new ACPropertyConfigValue<string>(this, "LogClearUrl", "control");
        }

        public override bool ACInit(Global.ACStartTypes startChildMode = Global.ACStartTypes.Automatic)
        {
            bool baseResult = base.ACInit(startChildMode);

            using (ACMonitor.Lock(_20015_LockValue))
            {
                _DelegateQueue = new ACDelegateQueue(this.GetACUrl());
            }
            _DelegateQueue.StartWorkerThread();

            _ = StoreRecivedData;
            _ = ExportDir;
            _ = FileName;
            _ = SensorMinCountValue;
            _ = LogOutputUrl;
            _ = LogMessageUrl;
            _ = LogClearUrl;

            if (!CanSend())
            {
                // [Error50573] ACRestClient not available!
                LogMessage(eMsgLevel.Error, "Error50573", nameof(ACInit), 56, null);
            }

            return baseResult;
        }

        public override bool ACDeInit(bool deleteACClassTask = false)
        {
            bool baseDeinit = base.ACDeInit(deleteACClassTask);

            _DelegateQueue.StopWorkerThread();
            using (ACMonitor.Lock(_20015_LockValue))
            {
                _DelegateQueue = null;
            }

            return baseDeinit;
        }

        #endregion

        #region Configuration

        private ACPropertyConfigValue<bool> _StoreRecivedData;
        [ACPropertyConfig("StoreRecivedData")]
        public bool StoreRecivedData
        {
            get
            {
                return _StoreRecivedData.ValueT;
            }
            set
            {
                _StoreRecivedData.ValueT = value;
            }
        }

        private ACPropertyConfigValue<string> _ExportDir;
        [ACPropertyConfig("ExportDir")]
        public string ExportDir
        {
            get
            {
                return _ExportDir.ValueT;
            }
            set
            {
                _ExportDir.ValueT = value;
            }
        }

        private ACPropertyConfigValue<string> _FileName;
        [ACPropertyConfig("FileName")]
        public string FileName
        {
            get
            {
                return _FileName.ValueT;
            }
            set
            {
                _FileName.ValueT = value;
            }
        }

        private ACPropertyConfigValue<int> _SensorMinCountValue;
        [ACPropertyConfig("SensorMinCountValue")]
        public int SensorMinCountValue
        {
            get
            {
                return _SensorMinCountValue.ValueT;
            }
            set
            {
                _SensorMinCountValue.ValueT = value;
            }
        }

        private ACPropertyConfigValue<string> _LogOutputUrl;
        [ACPropertyConfig("LogOutputUrl")]
        public string LogOutputUrl
        {
            get
            {
                return _LogOutputUrl.ValueT;
            }
            set
            {
                _LogOutputUrl.ValueT = value;
            }
        }

        private ACPropertyConfigValue<string> _LogMessageUrl;
        [ACPropertyConfig("LogMessageUrl")]
        public string LogMessageUrl
        {
            get
            {
                return _LogMessageUrl.ValueT;
            }
            set
            {
                _LogMessageUrl.ValueT = value;
            }
        }

        private ACPropertyConfigValue<string> _LogClearUrl;
        [ACPropertyConfig("LogClearUrl")]
        public string LogClearUrl
        {
            get
            {
                return _LogClearUrl.ValueT;
            }
            set
            {
                _LogClearUrl.ValueT = value;
            }
        }

        #endregion

        #region Binding properties

        [ACPropertyBindingTarget(100, "ActualValue", "en{'Actual Value'}de{'Actual Value'}", "", false, true)]
        public IACContainerTNet<Double> ActualValue { get; set; }

        [ACPropertyBindingSource(210, "Error", "en{'Reading Counter Alarm'}de{'Reading Counter Alarm'}", "", false, true)]
        public IACContainerTNet<PANotifyState> IsReadingCounterAlarm { get; set; }

        [ACPropertyBindingSource(211, "Error", "en{'Error-text'}de{'Fehlertext'}", "", false, true)]
        public IACContainerTNet<string> ErrorText { get; set; }

        #endregion

        #region Properties

        private JsonSerializerSettings _DefaultJsonSerializerSettings;
        public JsonSerializerSettings DefaultJsonSerializerSettings
        {
            get
            {
                if (_DefaultJsonSerializerSettings == null)
                    _DefaultJsonSerializerSettings = new JsonSerializerSettings
                    {
                        NullValueHandling = NullValueHandling.Ignore,
                        //TypeNameHandling = TypeNameHandling.None,
                        //DefaultValueHandling = DefaultValueHandling.Ignore,
                        Formatting = Newtonsoft.Json.Formatting.None
                    };

                return _DefaultJsonSerializerSettings;
            }
        }


        private ACRef<ACRestClient> _Client;
        public ACRestClient Client
        {
            get
            {
                if (_Client != null)
                    return _Client.ValueT;
                var client = FindChildComponents<ACRestClient>(c => c is ACRestClient).FirstOrDefault();
                if (client != null)
                {
                    // TODO: find right place for this
                    ServicePointManager.ServerCertificateValidationCallback = delegate (object s, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors) { return true; };
                    _Client = new ACRef<ACRestClient>(client, this);
                    return _Client.ValueT;
                }
                return null;
            }
        }

        private bool? _IsResetCounterSuccessfully;

        [ACPropertyInfo(true, 205, "", "en{'Reset successfully'}de{'Zurücksetzen erfolgreich'}", "", false)]
        public bool? IsResetCounterSuccessfully
        {
            get
            {
                return _IsResetCounterSuccessfully;
            }
            set
            {
                if (_IsResetCounterSuccessfully != value)
                {
                    _IsResetCounterSuccessfully = value;
                    OnPropertyChanged();
                }
            }
        }

        private ACDelegateQueue _DelegateQueue = null;
        public ACDelegateQueue DelegateQueue
        {
            get
            {

                using (ACMonitor.Lock(_20015_LockValue))
                {
                    return _DelegateQueue;
                }
            }
        }

        #endregion

        #region Methods

        #region Methods -> ACMethod


        [ACMethodInteraction("ResetCounter", "en{'Reset counter'}de{'Zähler zurücksetzen'}", 202, true)]
        public void ResetCounter()
        {
            ErrorText.ValueT = null;
            if (!IsEnabledResetCounter())
            {
                // [Error50573] ACRestClient not available!
                LogMessage(eMsgLevel.Error, "Error50573", nameof(ACInit), 276, null);
                return;
            }

            bool success = false;
            IsResetCounterSuccessfully = null;
            FilterClear filter = new FilterClear();
            string requestJson = JsonConvert.SerializeObject(filter, DefaultJsonSerializerSettings);
            using (var content = new StringContent(requestJson, Encoding.UTF8, "application/json"))
            {
                WSResponse<string> response = this.Client.Patch<string>(content, LogClearUrl);

                if (response.Suceeded)
                {
                    success = true;
                    ActualValue.ValueT = 0;
                }
                else
                {
                    // Error50574
                    // Error by resetting counter! Error {0}.
                    LogMessage(eMsgLevel.Error, "Error50574", nameof(ACInit), 276, response.Message?.Message);
                }
            }
            IsResetCounterSuccessfully = success;
        }

        public bool IsEnabledResetCounter()
        {
            return CanSend();
        }

        [ACMethodInteraction("ReadCounter", "en{'Count'}de{'Zählen'}", 203, true)]
        public void ReadCounter()
        {
            ErrorText.ValueT = null;
            WSResponse<int> result = new WSResponse<int>();
            if (!IsEnabledReadCounter())
            {
                // [Error50573] ACRestClient not available!
                LogMessage(eMsgLevel.Error, "Error50573", nameof(ACInit), 324, null);
            }
            
            WSResponse<Wise4000Data> dataResult = GetData(LogOutputUrl, LogMessageUrl);
            if (dataResult.Data != null && (dataResult.Message == null || dataResult.Message.MessageLevel < eMsgLevel.Failure))
            {
                result.Data = CountData(dataResult.Data);
                ActualValue.ValueT = result.Data;
                if (StoreRecivedData && !string.IsNullOrEmpty(ExportDir) && !string.IsNullOrEmpty(FileName) && Directory.Exists(ExportDir))
                {
                    ExportData(ExportDir, FileName, dataResult.Data);
                }
            }
            else
            {
                // Error50575
                // rror by reading counter! Error {0}.
                LogMessage(eMsgLevel.Error, "Error50575", nameof(ACInit), 342, dataResult.Message?.Message);
            }

            IsResetCounterSuccessfully = null;
        }

        public bool IsEnabledReadCounter()
        {

            return CanSend() && IsResetCounterSuccessfully != null && IsResetCounterSuccessfully.Value;
        }

        public virtual void ExportData(string exportDir, string fileName, Wise4000Data data)
        {
            try
            {
                string file = string.Format(fileName, DateTime.Now);
                string fullFileName = Path.Combine(exportDir, file);
                string json = JsonConvert.SerializeObject(data);
                File.WriteAllText(fullFileName, json);
            }
            catch (Exception ec)
            {
                Messages.LogException(GetACUrl(), "ExportData(10)", ec);
            }
        }

        #endregion

        #region Methods -> Others

        public virtual bool IsEnabledGetValues()
        {
            if (!CanSend())
                return false;
            return true;
        }

        public virtual WSResponse<long?> GetAmount(string logOutputUrl)
        {
            WSResponse<long?> result = new WSResponse<long?>();
            Filter filter = new Filter();
            string requestJson = JsonConvert.SerializeObject(filter, DefaultJsonSerializerSettings);
            using (var content = new StringContent(requestJson, Encoding.UTF8, "application/json"))
            {
                WSResponse<string> setFilterResponse = Client.Patch(content, logOutputUrl);

                if (setFilterResponse.Suceeded)
                {
                    WSResponse<Filter> responseFilter = Client.Get<Filter>(logOutputUrl);
                    if (responseFilter.Suceeded)
                    {
                        result.Data = responseFilter.Data.Amt;
                    }
                    else
                    {
                        result.Message = responseFilter.Message;
                    }
                }
                else
                {
                    result.Message = setFilterResponse.Message;
                }
            }
            return result;
        }

        public virtual WSResponse<Wise4000Data> GetData(string logOutputUrl, string logMessageUrl)
        {
            WSResponse<Wise4000Data> result = new WSResponse<Wise4000Data>();
            WSResponse<long?> amountResult = GetAmount(logOutputUrl);
            if (amountResult.Data != null && amountResult.Data > 0)
            {
                Filter filter = new Filter();
                filter.FltrEnum = FltrEnum.AmountFilter;
                filter.Amt = amountResult.Data.Value;
                string requestJson = JsonConvert.SerializeObject(filter, DefaultJsonSerializerSettings);
                using (var content = new StringContent(requestJson, Encoding.UTF8, "application/json"))
                {
                    WSResponse<string> setFilterResponse = Client.Patch(content, logOutputUrl);
                    if (setFilterResponse.Suceeded)
                    {
                        WSResponse<Wise4000Data> dataResponse = Client.Get<Wise4000Data>(logMessageUrl);
                        if (dataResponse.Suceeded)
                        {
                            result.Data = dataResponse.Data;
                        }
                        else
                        {
                            result.Message = dataResponse.Message;
                        }
                    }
                    else
                    {
                        result.Message = setFilterResponse.Message;
                    }
                }
            }
            else
            {
                result.Message = amountResult.Message;
            }
            return result;
        }

        public int CountData(Wise4000Data data)
        {
            int count = 0;
            if (data.LogMsg != null)
            {
                foreach (LogMsg logMsg in data.LogMsg)
                {
                    if (logMsg.Record != null)
                    {
                        foreach (int[] entry in logMsg.Record)
                        {
                            foreach (int subEntry in entry)
                            {
                                if (subEntry > SensorMinCountValue)
                                {
                                    count++;
                                }
                            }
                        }
                    }
                }
            }

            return count;
        }

        #endregion

        #endregion

        #region Private

        private bool CanSend()
        {
            return Client != null
                    && !string.IsNullOrEmpty(Client.ServiceUrl)
                    && !Client.ConnectionDisabled;
        }

        public Msg LogMessage(eMsgLevel level, string translationID, string methodName, int linie, params object[] parameter)
        {
            Msg msg = new Msg(this, eMsgLevel.Exception, this.ACType.ACIdentifier, methodName, linie, translationID, parameter);
            IsReadingCounterAlarm.ValueT = PANotifyState.AlarmOrFault;
            ErrorText.ValueT = msg.Message;
            Messages.LogWarning(this.GetACUrl(), nameof(PAEWiseBase), msg.Message);
            OnNewAlarmOccurred(IsReadingCounterAlarm, msg, true);
            return msg;
        }

        #endregion

    }
}
