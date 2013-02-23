﻿/*
 * Created by SharpDevelop.
 * User: Alexander Petrovskiy
 * Date: 29.11.2011
 * Time: 14:17
 * 
 * To change this template use Tools | Options | Coding | Edit Standard Headers.
 */

namespace UIAutomation
{
    using System;
    using System.Management.Automation;
    using System.Windows.Automation;
    using System.Runtime.InteropServices;
    using System.Diagnostics;
    using System.Windows.Automation;
    using System.Collections;
    using System.Linq;
    using System.Linq.Expressions;

    /// <summary>
    /// Description of HasTimeoutCmdletBase.
    /// </summary>
    public class HasTimeoutCmdletBase : HasControlInputCmdletBase
    {
        // http://pinvoke.net/default.aspx/user32.FindWindow
        [DllImport("user32.dll", EntryPoint="FindWindow", SetLastError = true)]
        private static extern System.IntPtr FindWindowByCaption(IntPtr ZeroOnly, string lpWindowName);
        // You can also call FindWindow(default(string), lpWindowName) or FindWindow((string)null, lpWindowName)
        
        #region Constructor
        public HasTimeoutCmdletBase()
        {
            //this.WriteVerbose(this, "constructor");
            
            this.Wait = true;
            this.Timeout = Preferences.Timeout;
            this.Seconds = Timeout / 1000;
            this.OnErrorScreenShot = Preferences.OnErrorScreenShot;
            this.OnSuccessAction = null;
            this.OnErrorAction = null;
        }
        #endregion Constructor

        #region Parameters
        [Parameter(Mandatory = false)]
        internal SwitchParameter Wait { get; set; }
        [Alias("Milliseconds")]
        [Parameter(Mandatory = false)]
        public int Timeout { get; set; }
        [Parameter(Mandatory = false)]
        public int Seconds {
            get { return Timeout / 1000; }
            set{ Timeout = value * 1000; }
        }
        
        // 20120830
        [Parameter(Mandatory = false)]
        public SwitchParameter IsCritical { get; set; }
        
        // 20120816
//        [Parameter(Mandatory = false)]
//        public ScriptBlock[] OnSleepAction { get; set; }
        #endregion Parameters
        
        protected override void StopProcessing()
        {
            WriteVerbose(this, "User interrupted");
            this.Wait = false;
        }
        
        internal ArrayList GetWindow(
            GetWindowCmdletBase cmdlet,
            Process[] processes,
            string[] processNames,
            int[] processIds,
            string[] windowNames,
            string automationId,
            string className)
        {
            ArrayList aeWndCollection = 
                new ArrayList();
            
            //WriteDebug(cmdlet, "getting the root element");
            cmdlet.WriteVerbose(cmdlet, "getting the root element");
            rootElement =
                System.Windows.Automation.AutomationElement.RootElement;
            if (rootElement == null)
            {
                cmdlet.WriteVerbose(cmdlet, "rootElement == null");
                return aeWndCollection;
            }
            else
            {
                //WriteDebug(cmdlet, "rootElement: " + rootElement.Current);
                cmdlet.WriteVerbose(cmdlet, "rootElement: " + rootElement.Current);
            }
            
            do {

                if (null != processes && processes.Length > 0) {

                    cmdlet.WriteVerbose(cmdlet, "getting a window by process");
                    aeWndCollection = getWindowCollectionFromProcess(processes, cmdlet.First, cmdlet.Recurse, cmdlet.Name, cmdlet.AutomationId, cmdlet.Class);

                } else if (null != processIds && processIds.Length > 0) {

                    cmdlet.WriteVerbose(cmdlet, "getting a window by PID");
                    aeWndCollection = getWindowCollectionByPID(processIds, cmdlet.First, cmdlet.Recurse, cmdlet.Name, cmdlet.AutomationId, cmdlet.Class);

                } else if (null != processNames && processNames.Length > 0) {

                    cmdlet.WriteVerbose(cmdlet, "getting a window by name");
                    aeWndCollection = getWindowCollectionByProcessName(processNames, cmdlet.First, cmdlet.Recurse, cmdlet.Name, cmdlet.AutomationId, cmdlet.Class);

                } else if ((null != windowNames && windowNames.Length > 0) ||
                           (null != automationId && 0 < automationId.Length) ||
                           (null != className && 0 < className.Length)) {

                    cmdlet.WriteVerbose(cmdlet, "getting a window by name, automationId, className");
                    aeWndCollection = getWindowCollectionByName(windowNames, automationId, className, cmdlet.Recurse);
                }

                if (null != aeWndCollection && aeWndCollection.Count > 0) {

                    //WriteDebug(cmdlet, "aeWndCollection != null");
                    cmdlet.WriteVerbose(cmdlet, "aeWndCollection != null");
                }
                // 20120123
                // checkTimeout(ref aeWnd, true);
                // 20120824
                //checkTimeout(cmdlet, aeWnd, true);
                checkTimeout(cmdlet, aeWndCollection, true);
                
                System.Threading.Thread.Sleep(Preferences.OnSleepDelay);
                
            } while (cmdlet.Wait);
            try {

                if (null != aeWndCollection && (int)((AutomationElement)aeWndCollection[0]).Current.ProcessId > 0) {

                    //WriteDebug(cmdlet, "" + aeWndCollection.ToString());
                    cmdlet.WriteVerbose(cmdlet, "" + aeWndCollection.ToString());
                    //WriteDebug(cmdlet, "aeWnd.Current.GetType() = " +
                    cmdlet.WriteVerbose(cmdlet, 
                                        "aeWnd.Current.GetType() = " +
                                        ((AutomationElement)aeWndCollection[0]).GetType().Name);

                } // 20120127

                CurrentData.CurrentWindow = (AutomationElement)aeWndCollection[aeWndCollection.Count -1];
                // 20120824
                //return aeWnd;
                //return aeWndCollection;
            } catch (Exception eEvaluatingWindowAndWritingToPipeline) {
                
                //WriteDebug(cmdlet, "exception: " +
                cmdlet.WriteVerbose(
                    cmdlet,
                    "exception: " +
                    eEvaluatingWindowAndWritingToPipeline.Message);

                cmdlet.WriteVerbose(this, "<<<< ==  ==  writing/nullifying CurrentWindow  ==  == >>>>>");

                CurrentData.CurrentWindow = null;
                
            }
            
            return aeWndCollection;
        }

        private ArrayList getWindowCollectionByProcessName(
            string[] processNames,
            bool first,
            bool recurse,
            string[] name,
            string automationId,
            string className)
        {

            ArrayList aeWndCollectionByProcId = 
                new ArrayList();
            
            System.Collections.ArrayList processIdList = 
                new System.Collections.ArrayList();
            
            foreach (string processName in processNames) {
            
                try {
                    
                    //WriteDebug(this, "getting process Id");
                    this.WriteVerbose(this, "getting process Id");
                    
                    System.Diagnostics.Process[] processes =
                        System.Diagnostics.Process.GetProcessesByName(processName);
                    // only the first
                    // 20120824
                    //processId = processes[0].Id;
                    // 20120824
                    //if (processId == 0) {
                    //    return aeWndByProcId;
                    //}
                    // 20120824
                    //WriteVerbose(this, "processId = " + processId.ToString());
                    // 20120824
                    //aeWndByProcId = getWindowCollectionByPID(processId);
                    
                    
                    // 20120824
                    //return aeWndByProcId;
                    
                    // 20120824
                    //processIdList.AddRange(processes);
                    foreach (Process process in processes) {
                        processIdList.Add(process.Id);
                    }
                }
                catch {
                    // 20120824
                    //return aeWndByProcId;
                    return aeWndCollectionByProcId;
                }
            
            } // 20120824
            
            int[] processIds = (int[])processIdList.ToArray(typeof(int));
            aeWndCollectionByProcId = getWindowCollectionByPID(processIds, first, recurse, name, automationId, className);
            
            return aeWndCollectionByProcId;
        }
        
        private ArrayList getWindowCollectionByPID(
            int[] processIds,
            bool first,
            bool recurse,
            string[] name,
            string automaitonId,
            string className)
        {
            System.Windows.Automation.AndCondition conditionsProcessId = null;
            // 20130223
            System.Windows.Automation.AndCondition conditionsForRecurseSearch = null;
            ArrayList aeWndCollectionByProcessId = 
                new ArrayList();
            
            // 20130223
            if ((null != name && 0 < name.Length) ||
                (null != automaitonId && string.Empty != automaitonId) ||
                (null != className && string.Empty != className)) {
                
                recurse = true;
            }
            
            foreach (int processId in processIds) {
            
                try {
                    
                    conditionsProcessId =
                        new System.Windows.Automation.AndCondition(
                            new System.Windows.Automation.PropertyCondition(
                                System.Windows.Automation.AutomationElement.ProcessIdProperty,
                                processId),
                            new System.Windows.Automation.OrCondition(
                                new System.Windows.Automation.PropertyCondition(
                                    System.Windows.Automation.AutomationElement.ControlTypeProperty,
                                    System.Windows.Automation.ControlType.Window),
                                new System.Windows.Automation.PropertyCondition(
                                    System.Windows.Automation.AutomationElement.ControlTypeProperty,
                                    System.Windows.Automation.ControlType.Pane),
                                new System.Windows.Automation.PropertyCondition(
                                    System.Windows.Automation.AutomationElement.ControlTypeProperty,
                                    System.Windows.Automation.ControlType.Menu)));
                    
                    if (recurse) {
                        conditionsForRecurseSearch =
                            new System.Windows.Automation.AndCondition(
                                new System.Windows.Automation.PropertyCondition(
                                    System.Windows.Automation.AutomationElement.ProcessIdProperty,
                                    processId),
                                new System.Windows.Automation.PropertyCondition(
                                    System.Windows.Automation.AutomationElement.ControlTypeProperty,
                                    System.Windows.Automation.ControlType.Window));
                    }
                    
                    WriteVerbose(this, "using processId = " +
                                 processId.ToString() +
                                 " and ControlType.Window or ControlType.Pane conditions");
                } catch { // 20120206  (Exception eCouldNotGetProcessId) {
                    // if (fromCmdlet) WriteDebug(this, "" + eCouldNotGetProcessId.Message);
                    
                    conditionsProcessId =
                        new System.Windows.Automation.AndCondition(
                            new System.Windows.Automation.PropertyCondition(
                                System.Windows.Automation.AutomationElement.ProcessIdProperty,
                                processId),
                            new System.Windows.Automation.OrCondition(
                                new System.Windows.Automation.PropertyCondition(
                                    System.Windows.Automation.AutomationElement.ControlTypeProperty,
                                    System.Windows.Automation.ControlType.Window),
                                new System.Windows.Automation.PropertyCondition(
                                    System.Windows.Automation.AutomationElement.ControlTypeProperty,
                                    System.Windows.Automation.ControlType.Pane),
                                new System.Windows.Automation.PropertyCondition(
                                    System.Windows.Automation.AutomationElement.ControlTypeProperty,
                                    System.Windows.Automation.ControlType.Menu)));
                    
                    if (recurse) {
                        conditionsForRecurseSearch =
                            new System.Windows.Automation.AndCondition(
                                new System.Windows.Automation.PropertyCondition(
                                    System.Windows.Automation.AutomationElement.ProcessIdProperty,
                                    processId),
                                new System.Windows.Automation.PropertyCondition(
                                    System.Windows.Automation.AutomationElement.ControlTypeProperty,
                                    System.Windows.Automation.ControlType.Window));
                    }
                    
                }
                
                WriteVerbose(this, "trying to get aeWndByProcId: by processId = " +
                             processId.ToString());
    
                try {
                    
                    //AutomationElementCollection tempCollection = null;
                    if (recurse) {
                        
                        if (first) {
                            
                            AutomationElement rootWindowElement =
                                rootElement.FindFirst(
                                    TreeScope.Children,
                                    conditionsProcessId);
                            
                            if (null != rootWindowElement) {
                                
                                aeWndCollectionByProcessId.Add(rootWindowElement);
                                
//                                AutomationElement lowerElement =
//                                    rootWindowElement.FindFirst(
//                                        TreeScope.Descendants,
//                                        conditionsForRecurseSearch);
//                                
//                                if (null != lowerElement) {
//                                    aeWndCollectionByProcessId.Add(lowerElement);
//                                }
                            }
                            
                        } else {
                        
                            AutomationElementCollection rootCollection =
                                rootElement.FindAll(
                                    TreeScope.Children,
                                    conditionsProcessId);
                            
                            if (null != rootCollection && 0 < rootCollection.Count) {
                                
                                aeWndCollectionByProcessId.AddRange(rootCollection);
                                
                                foreach (AutomationElement rootWindowElement in rootCollection) {
                                    
                                    AutomationElementCollection tempCollection = null;
                                    tempCollection =
                                        rootWindowElement.FindAll(
                                            TreeScope.Descendants,
                                            conditionsForRecurseSearch);
                                    
                                    if (null != tempCollection && 0 < tempCollection.Count) {
                                        aeWndCollectionByProcessId.AddRange(tempCollection);
                                    }
                                }
                            }
                        }
                        
                    } else {
                        
                        if (first) {
                            
                            AutomationElement tempElement =
                                rootElement.FindFirst(TreeScope.Children,
                                                      conditionsProcessId);
                            
                            if (null != tempElement) {
                                aeWndCollectionByProcessId.Add(tempElement);
                            }
                        } else {
                            
                            AutomationElementCollection tempCollection =
                                rootElement.FindAll(TreeScope.Children,
                                                    conditionsProcessId);
                            
                            if (null != tempCollection && 0 < tempCollection.Count) {
                                aeWndCollectionByProcessId.AddRange(tempCollection);
                            }
                        }
                    }
                    
                } catch (Exception eGetFirstChildOfRootByProcessId) {
                    WriteDebug(this, "exception: " +
                               eGetFirstChildOfRootByProcessId.Message);
                }
                // 20120824
                //if (aeWndByProcId != null &&
                if (null != aeWndCollectionByProcessId &&
                    // 20120824
                    //(int)aeWndByProcId.Current.ProcessId > 0) {
                    //(int)((AutomationElement)aeWndCollectionByProcessId[0]).Current.ProcessId > 0) {
                    processId > 0) {
                    WriteVerbose(this, "aeWndByProcId: " +
                                 // 20120824
                                 //aeWndByProcId.Current.Name +
                                 //((AutomationElement)aeWndCollectionByProcessId[0]).Current.Name +
                                 "is caught by processId = " + processId.ToString());
                }
                else{
                    WriteDebug(this, "aeWndByProcId is still null");
                }
            
            } // 20120824
            
            return aeWndCollectionByProcessId;
        }
        
        private ArrayList getWindowCollectionFromProcess(
            Process[] processes,
            bool first,
            bool recurse,
            string[] name,
            string automationId,
            string className)
        {
            int processId = 0;
            ArrayList aeWndCollectionByProcId = 
                new ArrayList();
            ArrayList tempCollection = 
                new ArrayList();
            
            System.Collections.ArrayList processIdList = 
                new System.Collections.ArrayList();
            int[] processIds = null;
            
            foreach (Process process in processes) {
            
            try {
                processId = process.Id;
                if (0 != processId) {
                    processIdList.Add(processId);
                }
                
                WriteVerbose(this, "processId = " + processId.ToString());

                processIds = (int[])processIdList.ToArray(typeof(int));
                
                if (null == processIds) {
                    WriteVerbose(this, "!!!!!!!!!!!!!!!!!!!! null == processIds !!!!!!!!!");
                }
                
                tempCollection = getWindowCollectionByPID(processIds, first, recurse, name, automationId, className);
                
                foreach (AutomationElement tempElement in tempCollection) {
                    aeWndCollectionByProcId.Add(tempElement);
                }
            }
            catch (Exception tempException) {
                
                WriteVerbose(this, tempException.Message);
                
                //return aeWndCollectionByProcId;
            }
            
            } // 20120824

            return aeWndCollectionByProcId;
        }
        
        private ArrayList getWindowCollectionByName(string[] windowNames, string automationId, string className, bool recurse)
        {
            System.Windows.Automation.OrCondition conditionsSet = null;
            
            System.Collections.ArrayList aeWndCollectionByTitle = 
                new System.Collections.ArrayList();
            System.Collections.ArrayList resultCollection = 
                new System.Collections.ArrayList();
            
            // 20130220
            if (null == windowNames) {
                windowNames = new string[]{ string.Empty };
            }
            
            // 20120824
            foreach (string windowTitle in windowNames) {
            
                WriteVerbose(this, "processName.Length == 0");
                #region changed 20120608
                //            conditionsTitle =
                //                new AndCondition(
                //                    new System.Windows.Automation.OrCondition(
                //                        new System.Windows.Automation.PropertyCondition(
                //                            System.Windows.Automation.AutomationElement.ControlTypeProperty,
                //                            System.Windows.Automation.ControlType.Window),
                //                        new System.Windows.Automation.PropertyCondition(
                //                            System.Windows.Automation.AutomationElement.ControlTypeProperty,
                //                            System.Windows.Automation.ControlType.Pane),
                //                        new System.Windows.Automation.PropertyCondition(
                //                            System.Windows.Automation.AutomationElement.ControlTypeProperty,
                //                            System.Windows.Automation.ControlType.Menu)),
                //                    new PropertyCondition(
                //                        System.Windows.Automation.AutomationElement.NameProperty,
                //                        title));
                #endregion changed 20120608
                
                // 20130221
                if (recurse) {
                    conditionsSet =
                        new System.Windows.Automation.OrCondition(
                            new System.Windows.Automation.PropertyCondition(
                                System.Windows.Automation.AutomationElement.ControlTypeProperty,
                                System.Windows.Automation.ControlType.Window),
                            System.Windows.Automation.Condition.FalseCondition);
                } else {
                    conditionsSet =
                        new System.Windows.Automation.OrCondition(
                            new System.Windows.Automation.PropertyCondition(
                                System.Windows.Automation.AutomationElement.ControlTypeProperty,
                                System.Windows.Automation.ControlType.Window),
                            new System.Windows.Automation.PropertyCondition(
                                System.Windows.Automation.AutomationElement.ControlTypeProperty,
                                System.Windows.Automation.ControlType.Pane),
                            new System.Windows.Automation.PropertyCondition(
                                System.Windows.Automation.AutomationElement.ControlTypeProperty,
                                System.Windows.Automation.ControlType.Menu));
                }
                WriteVerbose(this, "trying to get window: by title = " +
                             // 20120824
                             //title);
                             windowTitle);
                #region changed 20120608
                //            aeWndByTitle =
                //                rootElement.FindFirst(System.Windows.Automation.TreeScope.Children,
                //                                    conditionsTitle);
                #endregion changed 20120608
                
                // 20130221
                AutomationElementCollection aeWndCollection = null;
                if (recurse) {
                    aeWndCollection =
                        rootElement.FindAll(TreeScope.Descendants,
                                            conditionsSet);
                } else {
                    //AutomationElementCollection aeWndCollection =
                    aeWndCollection =
                        rootElement.FindAll(TreeScope.Children,
                                            conditionsSet);
                }
                
#region commented
                // 20130220
    //            WildcardOptions options;
    //            //            if (caseSensitive) {
    //            //                options =
    //            //                    WildcardOptions.Compiled;
    //            //            } else {
    //            options =
    //                WildcardOptions.IgnoreCase |
    //                WildcardOptions.Compiled;
    //            //            }
    //            WildcardPattern wildcardName =
    //                // 20120824
    //                //new WildcardPattern(title,options);
    //                new WildcardPattern(windowTitle, options);
    //
    //            
    ////            bool matched = false;
    ////            if (name.Length > 0 && wildcardName.IsMatch(element.Current.Name)) {
    ////                matched = true;
    ////            } else if (automationId.Length > 0 &&
    ////                       wildcardAutomationId.IsMatch(element.Current.AutomationId)) {
    ////                matched = true;
    ////            } else if (className.Length > 0 &&
    ////                       wildcardClass.IsMatch(element.Current.ClassName)) {
    ////                matched = true;
    ////            }
    ////
    ////            if (matched) {
    ////                if (onlyOneResult) {
    ////                    result = element;
    ////                } else {
    ////                    WriteObject(this, element);
    ////                }
    ////            }
    //            
    //            if (aeWndCollection.Count > 0) {
    //    
    //                WriteVerbose(this, "aeWndCollection.Count > 0");
    //                
    //                foreach (AutomationElement wndInColleciton in aeWndCollection) {
    //                    if (wndInColleciton.Current.Name != null && 
    //                        wndInColleciton.Current.Name != string.Empty &&
    //                        wndInColleciton.Current.Name.Length > 0 &&
    //                        wildcardName.IsMatch(wndInColleciton.Current.Name)) {
    //                        // 20120824
    //                        //aeWndByTitle = wndInColleciton;
    //                        //break;
    //                        aeWndCollectionByTitle.Add(wndInColleciton);
    //                    }
    //                }
    //            }
    ////
    //else {
    //    WriteVerbose(this, "aeWndCollection.Count == 0");
    //}
    ////
    
                
    //            if (null != aeWndCollection && 0 < aeWndCollection.Count) {
    //                
    //                System.Collections.ArrayList tempCollection = 
    //                    new System.Collections.ArrayList();
    //                System.Windows.Automation.AndCondition conditionsForWildCards =
    //                    this.getControlConditions(this, string.Empty, false, true) as AndCondition;
    //                
    //                SearchByWildcardViaUIA((GetCmdletBase)this, ref tempCollection, AutomationElement.RootElement, windowTitle, automationId, className, string.Empty, conditionsForWildCards);
    //                
    //                if (null != tempCollection && 0 < tempCollection.Count) {
    //                    aeWndCollectionByTitle.AddRange(tempCollection);
    //                }
    //            }
#endregion commented
    
                this.WriteVerbose(this, "trying to run the returnOnlyRightElements method");
                this.WriteVerbose(this, "collected " + aeWndCollection.Count.ToString() + " elements for further selection");
                
                aeWndCollectionByTitle =
                    //(this as GetCmdletBase).returnOnlyRightElements(
                    this.returnOnlyRightElements(
                        aeWndCollection,
                        windowTitle,
                        automationId,
                        className,
                        string.Empty,
                        (new string[]{ "Window", "Pane", "Menu" }),
                        false);
                
                this.WriteVerbose(this, "after running the returnOnlyRightElements method");
                this.WriteVerbose(this, "collected " + aeWndCollectionByTitle.Count.ToString() + " elements");
                
                // 20120831
                if (null != aeWndCollectionByTitle && 0 < aeWndCollectionByTitle.Count) {
                    // 20120824
                    foreach (AutomationElement aeWndByTitle in aeWndCollectionByTitle) {
                        
                        // 20120831
                        //AutomationElement tempElement = null;
                    
                        if (aeWndByTitle != null &&
                            (int)aeWndByTitle.Current.ProcessId > 0) {
                            WriteVerbose(this, "aeWndByTitle: " +
                                         aeWndByTitle.Current.Name +
                                         // 20120824
                                         //" is caught by title = " + title);
                                         " is caught be title = " + windowTitle);
                                
                                // 20120824
                                resultCollection.Add(aeWndByTitle);
                                
                        } 
                        
                    } // 20120831
                    
                } else {
                    
                    // 20120831
                    AutomationElement tempElement = null;
                    
                    // one more attempt using the FindWindow function
                    System.IntPtr wndHandle =
                        // 20120824
                        //FindWindowByCaption(System.IntPtr.Zero, title);
                        FindWindowByCaption(System.IntPtr.Zero, windowTitle);
                    if (wndHandle != null && wndHandle != System.IntPtr.Zero) {
                        // 20120824
                        //aeWndByTitle =
                        tempElement =
                            AutomationElement.FromHandle(wndHandle);
                    }
                    // 20120824
                    //if (aeWndByTitle != null && (int)aeWndByTitle.Current.ProcessId > 0) {
                    if (null != tempElement && (int)tempElement.Current.ProcessId > 0) {
                        WriteVerbose(this, "aeWndByTitle: " +
                                     // 20120824
                                     //aeWndByTitle.Current.Name +
                                     // 20120824
                                     //" is caught by title = " + title +
                                     " is caught by title = " + windowTitle +
                                     " using the FindWindow function");
                        
                        // 20120824
                        resultCollection.Add(tempElement);
                        
                    }
                }
                
                // 20120831                
                //} // 20120824
            
            } // 20120824
            
            // 20120824
            //return aeWndByTitle;
            return resultCollection;
        }
        
        // 20120123
        // private void checkTimeout(ref System.Windows.Automation.AutomationElement aeWindow,
        private void checkTimeout(GetWindowCmdletBase cmdlet,
                                  //System.Windows.Automation.AutomationElement aeWindow,
                                  ArrayList aeWindowList,
                                  bool fromCmdlet)
        {
            // RunOnRefreshScriptBlocks(this);
            // System.Threading.Thread.Sleep(Preferences.SleepInterval);
            // RunScriptBlocks(this);
            SleepAndRunScriptBlocks(this);
            System.DateTime nowDate = System.DateTime.Now;
            WriteVerbose(this, "process: " +
                         // processName +
                         cmdlet.ProcessName +
                         ", name: " +
                         cmdlet.Name +
                         ", seconds: " + (nowDate - startDate).TotalSeconds);
            try {
                // 20120824
                //if ((aeWindow != null && (int)aeWindow.Current.ProcessId > 0) ||
                if ((null != aeWindowList && //(int)((AutomationElement)aeWindowList[0]).Current.ProcessId > 0) ||
                     aeWindowList.Count > 0) ||
                    (nowDate - startDate).TotalSeconds > this.Timeout / 1000)
                {
                    // 20120824
                    //if (aeWindow == null) {
                    if (null == aeWindowList) {
                        //                        ErrorRecord err =
                        //                            new ErrorRecord(
                        //                                new Exception(),
                        //                                "ControlIsNull",
                        //                                ErrorCategory.OperationTimeout,
                        //                                aeWindow);
                        //                        err.ErrorDetails =
                        //                            new ErrorDetails(
                        //                                CmdletName(this) + ": timeout expired for process: " +
                        //                                cmdlet.ProcessName + ", title: " + cmdlet.Name);
                        //                        WriteError(this, err, false);
                        //                        //WriteError(this, err, true); //// 20120306
                        //return;
                        this.Wait = false;
                        Exception eReturn =
                            new Exception(
                                CmdletName(this) + ": timeout expired for process: " +
                                cmdlet.ProcessName + ", title: " + cmdlet.Name);
                        throw(eReturn);
                    }else{
//                        WriteVerbose(this, "got the window: " +
//                                     // 20120824
//                                     //aeWindow.Current.Name);
//                                     ((AutomationElement)aeWindowList[0]).Current.Name);
                    }
                    this.Wait = false;
                    // break;
                }
            } catch (Exception eEvaluatingWindowOrMeasuringTimeout) {
                // try { WriteDebug(CmdletName(this) + ": exception: " +
                // eEvaluatingWindowOrMeasuringTimeout.Message); } catch { }
                WriteDebug(this, "exception: " +
                           eEvaluatingWindowOrMeasuringTimeout.Message);
                
                // 20121001
                //UIAHelper.GetScreenshotOfAutomationElement(this, CmdletName(this) + "_Timeout", true, 0, 0, 0, 0, string.Empty, System.Drawing.Imaging.ImageFormat.Jpeg);
                cmdlet.WriteError(
                    cmdlet,
                    "An error raised during checking the timeout. " +
                    eEvaluatingWindowOrMeasuringTimeout.Message,
                    "CheckingTimeout",
                    ErrorCategory.InvalidOperation,
                    true);
            }
        }
        
        internal ArrayList returnOnlyRightElements(
            AutomationElementCollection results,
            string name,
            string automationId,
            string className,
            string textValue,
            string[] controlType,
            bool caseSensitive)
        {
            
            ArrayList resultCollection = new ArrayList();

            WildcardOptions options;
            if (caseSensitive) {
                options =
                    WildcardOptions.Compiled;
            } else {
                options =
                    WildcardOptions.IgnoreCase |
                    WildcardOptions.Compiled;
            }
            
            if (null == name || string.Empty == name || 0 == name.Length) { name = "*"; }
            if (null == automationId || string.Empty == automationId || 0 == automationId.Length) { automationId = "*"; }
            if (null == className || string.Empty == className || 0 == className.Length) { className = "*"; }
            if (null == textValue || string.Empty == textValue || 0 == textValue.Length) { textValue = "*"; }

            WildcardPattern wildcardName = 
                new WildcardPattern(name, options);
            WildcardPattern wildcardAutomationId = 
                new WildcardPattern(automationId, options);
            WildcardPattern wildcardClass = 
                new WildcardPattern(className, options);
            WildcardPattern wildcardValue = 
                new WildcardPattern(textValue, options);
            this.WriteVerbose(this, "inside the returnOnlyRightElements method 20");
                System.Collections.Generic.List<AutomationElement> list =
                    new System.Collections.Generic.List<AutomationElement>();
                foreach (AutomationElement elt in results) {

                    list.Add(elt);

                }

            try {
                
                var query = list
//                    .Where<AutomationElement>(item => wildcardName.IsMatch(item.Current.Name))
//                    .Where<AutomationElement>(item => wildcardAutomationId.IsMatch(item.Current.AutomationId))
//                    .Where<AutomationElement>(item => wildcardClass.IsMatch(item.Current.ClassName))
//                    .Where<AutomationElement>(item => 
//                                              item.GetSupportedPatterns().Contains(ValuePattern.Pattern) ? 
//                                              //(("*" != textValue) ? wildcardValue.IsMatch((item.GetCurrentPattern(ValuePattern.Pattern) as ValuePattern).Current.Value) : false) :
//                                              (("*" != textValue) ? (positiveMatch(item, wildcardValue, textValue)) : (negativeMatch(textValue))) :
//                                              true
//                                              )
                    .Where<AutomationElement>(
                        item => (wildcardName.IsMatch(item.Current.Name) &&
                                 wildcardAutomationId.IsMatch(item.Current.AutomationId) &&
                                 wildcardClass.IsMatch(item.Current.ClassName) &&
                                 // check whether a control has or hasn't ValuePattern
                                 (item.GetSupportedPatterns().Contains(ValuePattern.Pattern) ?
                                  testRealValueAndValueParameter(item, name, automationId, className, textValue, wildcardValue) :
                                  // check whether the -Value parameter has or hasn't value
                                  ("*" == textValue ? true : false)
                                 )
                                )
                       )
                    .ToArray<AutomationElement>();

                this.WriteVerbose(
                    this,
                    "There are " +
                    query.Count().ToString() +
                    " elements");

                resultCollection.AddRange(query);

                this.WriteVerbose(
                    this,
                    "There are " +
                    resultCollection.Count.ToString() +
                    " elements");
                
            }
            catch {
                
            }
            
            return resultCollection;
        }
        
        private bool testRealValueAndValueParameter(
            AutomationElement item,
            string name,
            string automationId,
            string className,
            string textValue,
            WildcardPattern wildcardValue)
        {
            bool result = false;
            
            // getting the real value of a control
            string realValue = string.Empty;
            try {
                realValue =
                    (item.GetCurrentPattern(ValuePattern.Pattern) as ValuePattern).Current.Value;
            }
            catch {}
            
            // if a control's ValuePattern has no value
            if (string.Empty == realValue) {
                
                if ("*" != name || "*" != automationId || "*" != className) {
                    return true;
                }
                return result;
            }
            
            // if there was not specified the -Value parameter
            if ("*" == textValue) {
                
                if ("*" != name || "*" != automationId || "*" != className) {
                    return true;
                }
                
                // 20130208
                //return result;
            }
            
            result =
                wildcardValue.IsMatch(realValue);

            return result;
        }
    }
}
