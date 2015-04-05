﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.ObjectModel;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Text.Operations;
using Microsoft.VisualStudio.Utilities;

namespace NimStudio.NimStudio {

    [Export(typeof(ISignatureHelpSourceProvider))]
    [Name("ToolTip SignatureHelp Source")]
    [ContentType(NSConst.LangName)]
    internal class NSSigSourceProvider: ISignatureHelpSourceProvider {
        [Import] internal ITextStructureNavigatorSelectorService NavigatorService { get; set; }
        [Import] internal ITextBufferFactoryService TextBufferFactoryService { get; set; }

        public ISignatureHelpSource TryCreateSignatureHelpSource(ITextBuffer textBuffer) {
            return new NSSigSource(this, textBuffer);
        }
    }

    internal class NSSigSource: ISignatureHelpSource {

        internal ITextBuffer m_textBuffer;

        public NSSigSource(ITextBuffer textBuffer) {
            m_textBuffer = textBuffer;
        }

        public class NSSigParameter: IParameter {
            internal Span m_trackingspan;
            internal string m_content;
            internal string m_prettyprintedcontent;
            internal string m_documentation;
            internal ISignature m_sig;
            public Span m_pplocus;

            //string documentation, Span locus, string name, ISignature signature
            internal NSSigParameter(Span trackingspan, string content, string prettyprintedcontent, string documentation, ISignature sig) {
            //public NSSigParameter(string Documentation, Span Locus, string Name, Span PPLocus, ISignature Signature) {
                m_trackingspan = trackingspan;
                m_content = content;
                m_prettyprintedcontent = prettyprintedcontent;
                m_documentation = documentation;
                m_pplocus = new Span();
                m_sig = sig;
                //m_trackingspan = trackingspan;
                //m_content = content;
                //m_prettyprintedcontent = prettyprintedcontent;
                //m_documentation = documentation;
                //PrettyPrintedLocus = PPLocus;
                //m_sig = sig;
            }

            public string Documentation { get { return m_documentation; } }
            public Span Locus { get { return m_trackingspan; } }
            public string Name { get { return m_documentation; } }
            public Span PrettyPrintedLocus { get { return m_pplocus; } }
            public ISignature Signature { get { return m_sig; } }

        }

        public class NSSignature: ISignature {

            internal ITextBuffer m_subjectBuffer;
            internal IParameter m_currentParameter;
            internal string m_content;
            internal string m_documentation;
            internal ITrackingSpan m_applicabletospan;
            internal ReadOnlyCollection<IParameter> m_parameters;
            internal string m_printContent;
            internal ISignatureHelpSession m_session;
            public event EventHandler<CurrentParameterChangedEventArgs> CurrentParameterChanged;

            public string PrettyPrintedContent { get { return m_printContent; } }
            public ITrackingSpan ApplicableToSpan { get { return m_applicabletospan; } }
            public string Content { get { return m_content; } }
            public string Documentation { get { return m_documentation; } }
            public ReadOnlyCollection<IParameter> Parameters { get { return m_parameters; } }

            internal NSSignature(ITextBuffer subject_buff, string content, string doc, ReadOnlyCollection<IParameter> parameters) {
                m_subjectBuffer = subject_buff;
                m_content = content;
                m_documentation = doc;
                m_parameters = parameters;
                m_subjectBuffer.Changed += new EventHandler<TextContentChangedEventArgs>(OnSubjectBufferChanged);
            }

            public IParameter CurrentParameter {
                get { return m_currentParameter; }
                internal set {
                    if (m_currentParameter != value) {
                        IParameter prevCurrentParameter = m_currentParameter;
                        m_currentParameter = value;
                        this.RaiseCurrentParameterChanged(prevCurrentParameter, m_currentParameter);
                    }
                }
            }

            private void RaiseCurrentParameterChanged(IParameter param_curr_prev, IParameter param_curr_new) {
                EventHandler<CurrentParameterChangedEventArgs> param_handler_temp = this.CurrentParameterChanged;
                if (param_handler_temp != null) {
                    param_handler_temp(this, new CurrentParameterChangedEventArgs(param_curr_prev, param_curr_new));
                }
            }

            internal void OnSubjectBufferChanged(object sender, TextContentChangedEventArgs e) {
                this.ParamCurrentCalc();
            }

            internal void ParamCurrentCalc() {
                NSUtil.DebugPrintAlways("ComputeCurrentParameter");
                if (m_parameters.Count == 0) {
                    this.CurrentParameter = null;
                    return;
                }

                //SnapshotPoint point_curr = m_session.GetTriggerPoint(m_subjectBuffer).GetPoint(m_subjectBuffer.CurrentSnapshot);
                SnapshotPoint point_curr = m_session.TextView.Caret.Position.BufferPosition;
                SnapshotPoint point_left = m_applicabletospan.GetStartPoint(m_subjectBuffer.CurrentSnapshot);
                SnapshotPoint point_test = point_curr-1;
                //point_curr = m_session.ge
                //SnapshotPoint currentPoint = new SnapshotPoint(m_subjectBuffer.CurrentSnapshot, m_session.TextView.Caret.Position.Point);
                SnapshotPoint currentPoint = m_session.TextView.Caret.Position.BufferPosition;


                string sig_str = m_applicabletospan.GetText(m_subjectBuffer.CurrentSnapshot);
                int commas_count = 0;
                int sig_idx = 0;
                while (true) {
                    if (point_test <= point_left) {
                        break;
                    }
                    if (point_test.GetChar() == ',') {
                        commas_count+=1;
                    }
                    point_test -= 1;
                }

                if (commas_count < m_parameters.Count) {
                    this.CurrentParameter = m_parameters[commas_count];
                    NSUtil.DebugPrintAlways("ComputeCurrentParameter Current:" + commas_count.ToString() + ":" + point_curr.Position.ToString() + ":" + currentPoint.Position.ToString());
                } else {
                    this.CurrentParameter = m_parameters[m_parameters.Count - 1];
                }
                return;

                //m_applicabletospan.
                //int sig_idx = 0;
                while (sig_idx < sig_str.Length) {
                    int comma_idx = sig_str.IndexOf(',', sig_idx);
                    if (comma_idx == -1) {
                        break;
                    }
                    commas_count++;
                    sig_idx = comma_idx + 1;
                }

                if (commas_count < m_parameters.Count) {
                    this.CurrentParameter = m_parameters[commas_count];
                    NSUtil.DebugPrintAlways("ComputeCurrentParameter Current:" + commas_count.ToString());
                } else {
                    //too many commas, so use the last parameter as the current one. 
                    this.CurrentParameter = m_parameters[m_parameters.Count - 1];
                }
            }

        }

        public void AugmentSignatureHelpSession(ISignatureHelpSession session, IList<ISignature> signatures) {
            //if (!session.TextView.Properties.ContainsProperty(SessionKey)) {
            //    return;
            //}
            signatures.Clear();
            var caretpos = NSLangServ.CaretPosGet();
            NSPackage.nimsuggest.Query(NimSuggestProc.Qtype.con, caretpos["line"], caretpos["col"]);

            SnapshotPoint? point_trigger = session.GetTriggerPoint(subjectBuffer.CurrentSnapshot);
            if (!point_trigger.HasValue) {
                return;
            }

            //ITextSnapshot snapshot = subjectBuffer.CurrentSnapshot;
            //int position = session.GetTriggerPoint(subjectBuffer).GetPosition(subjectBuffer.CurrentSnapshot);
            int position = point_trigger.Value.Position;
            
            string text = subjectBuffer.CurrentSnapshot.GetText();

            SnapshotPoint point_curr = session.GetTriggerPoint(subjectBuffer).GetPoint(subjectBuffer.CurrentSnapshot);
            point_curr = point_trigger.Value;
            SnapshotPoint point_left = point_curr;
            SnapshotPoint point_right = point_curr;

            ITextSnapshotLine line = point_left.GetContainingLine();
            string terms = "\n\r\t.:()[]{}?/+-;=*!<>";
            while (true) {
                if (point_left<=line.Start) {
                    point_left=line.Start;
                    break;
                }
                if (terms.IndexOf(point_left.GetChar()) != -1) {
                    point_left += 1;
                    break;
                }
                point_left -= 1;
            }
            while (true) {
                if (point_right>=line.End) {
                    point_right=line.End;
                    break;
                }
                if (terms.IndexOf(point_right.GetChar()) != -1) {
                    point_right -= 1;
                    break;
                }
                point_right += 1;
            }
            ITrackingSpan applicable_to_span = subjectBuffer.CurrentSnapshot.CreateTrackingSpan(point_left, point_right - point_left, SpanTrackingMode.EdgeInclusive);
            //ITrackingSpan applicableToSpan = subjectBuffer.CurrentSnapshot.CreateTrackingSpan(new Span(position, 0), SpanTrackingMode.EdgeInclusive, 0);

            foreach (string skey in NSPackage.nimsuggest.sugdct.Keys) {
                var sigdct = NSPackage.nimsuggest.sugdct[skey];
                signatures.Add(SigAdd(subjectBuffer, skey, sigdct["help"], applicable_to_span, session));
            }

            //var currentSnapshot = subjectTriggerPoint.Value.Snapshot;
            //var querySpan = new SnapshotSpan(subjectTriggerPoint.Value, 0);
            //var applicableToSpan = currentSnapshot.CreateTrackingSpan(querySpan.Start.Position, 0, SpanTrackingMode.EdgeInclusive);
            NSUtil.DebugPrintAlways("Sighelp");

            //signatures.Add(CreateSignature(subjectBuffer, "add(int firstInt, int secondInt)", "Documentation for adding integers.", applicableToSpan));
            //signatures.Add(CreateSignature(subjectBuffer, "add(double firstDouble, double secondDouble)", "Documentation for adding doubles.", applicableToSpan));

            //string sighelp = "hey1";
            //if (sighelp != null) {
            //}
        }

        //internal static readonly object SessionKey = new object();

        readonly ITextBuffer subjectBuffer;

        public NSSigSource(NSSigSourceProvider provider, ITextBuffer subjectBuffer) {
            this.subjectBuffer = subjectBuffer;
        }

        bool isDisposed;

        public void Dispose() {
            if (!isDisposed) {
                GC.SuppressFinalize(this);
                isDisposed = true;
            }
        }


        public ISignature GetBestMatch(ISignatureHelpSession session) {
            return session.Signatures.Count > 0 ? session.Signatures[0] : null;
        }

        private NSSignature SigAdd(ITextBuffer textbuff, string sigstr_passed, string docstr, ITrackingSpan span, ISignatureHelpSession session) {

            int parspot = sigstr_passed.LastIndexOf(')');
            if (parspot == -1) return null;

            NSSignature sig = new NSSignature(textbuff, sigstr_passed, docstr, null);
            textbuff.Changed += new EventHandler<TextContentChangedEventArgs>(sig.OnSubjectBufferChanged);

            string sigstr = sigstr_passed.Substring(0,parspot+1);
            string[] param_arr = sigstr.Split(new char[] { '(', ',', ')' });
            List<IParameter> param_lst = new List<IParameter>();

            // loop param array
            int sigstr_idx = 0;
            for (int i = 1; i < param_arr.Length; i++) {
                string param_str = param_arr[i].Trim();
                if (string.IsNullOrEmpty(param_str)) continue;

                // add param to list
                int param_idx = sigstr.IndexOf(param_str, sigstr_idx);
                if (param_idx >= 0) {
                    Span param_span = new Span(param_idx, param_str.Length);
                    sigstr_idx = param_idx + param_str.Length;
                    param_lst.Add(new NSSigParameter(param_span, param_str, "pp", "Documentation for the parameter.", sig));
                }
            }

            sig.m_parameters = new ReadOnlyCollection<IParameter>(param_lst);
            sig.m_applicabletospan = span;
            sig.m_session = session;
            sig.ParamCurrentCalc();
            return sig;
        }
    
    }

}
