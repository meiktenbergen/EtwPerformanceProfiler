//--------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// THIS CODE AND INFORMATION IS PROVIDED "AS IS" WITHOUT WARRANTY
// OF ANY KIND, EITHER EXPRESSED OR IMPLIED, INCLUDING BUT NOT
// LIMITED TO THE IMPLIED WARRANTIES OF MERCHANTABILITY AND
// FITNESS FOR A PARTICULAR PURPOSE.
//--------------------------------------------------------------------------

using System;
using System.Collections.Generic;

namespace EtwPerformanceProfiler
{
    /// <summary>
    /// Represents the performance profiler class to be used in AL.
    /// </summary>
    public class EtwPerformanceProfiler : IDisposable
    {
        /// <summary>
        /// The associated event processor.
        /// </summary>
        private DynamicProfilerEventProcessor dynamicProfilerEventProcessor;

        /// <summary>
        /// The call tree of all the aggregated method and SQL statement calls parsed from the ETW events
        /// </summary>
        private IEnumerator<AggregatedEventNode> callTree;

        /// <summary>
        /// Max relative time stamp for the currect profiling session.
        /// </summary>
        private double maxRelativeTimeStamp;

        public int CallTreeCurrentStatementSessionId
        {
            get 
            {
                return this.callTree.Current.SessionId;
            }
        }

        /// <summary>
        /// Gets the call tree's current statement's owning object id.
        /// </summary>
        public int CallTreeCurrentStatementOwningObjectId
        {
            get
            {
                return this.callTree.Current.ObjectId;
            }
        }

        /// <summary>
        /// Gets the call tree's current statement.
        /// </summary>
        public string CallTreeCurrentStatement
        {
            get
            {
                return this.callTree.Current.StatementName;
            }
        }

        /// <summary>
        /// Gets the current line number on the call tree.
        /// </summary>
        public int CallTreeCurrentStatementLineNo
        {
            get
            {
                return this.callTree.Current.LineNo;
            }
        }

        /// <summary>
        /// Gets call tree's current statements duration in miliseconds
        /// </summary>
        public long CallTreeCurrentStatementDurationMs
        {
            get
            {
                return (long)this.callTree.Current.DurationMSec;
            }
        }

        /// <summary>
        /// Gets call tree's current statements min duration in miliseconds
        /// </summary>
        public long CallTreeCurrentStatementMinDurationMs
        {
            get
            {
                return (long)this.callTree.Current.MinDurationMSec;
            }
        }

        /// <summary>
        /// Gets call tree's current statements max duration in miliseconds
        /// </summary>
        public long CallTreeCurrentStatementMaxDurationMs
        {
            get
            {
                return (long)this.callTree.Current.MaxDurationMSec;
            }
        }

        /// <summary>
        /// Gets call tree's current statements duration from the end of profiling.
        /// </summary>
        public long CallTreeCurrentStatementLastActiveMs
        {
            get
            {
                return (long)(this.maxRelativeTimeStamp - (long)this.callTree.Current.MaxRelativeTimeStampMSec);
            }
        }

        /// <summary>
        /// Gets the call tree' current current statement's depth.
        /// </summary>
        public int CallTreeCurrentStatementIndentation
        {
            get
            {
                return this.callTree.Current.Depth;
            }
        }

        /// <summary>
        /// Gets the current object type on the call tree. 
        /// </summary>
        public int CallTreeCurrentStatementOwningObjectType
        {
            get
            {
                // "TableData","Table","Form","Report","Dataport","Codeunit","XMLport","MenuSuite","Page","Query","System","FieldNumber",,,"PageExtension","TableExtension","Enum","EnumExtension","Profile","ProfileExtension","PermissionSet","PermissionSetExtension","ReportExtension";

                string objectType = this.callTree.Current.ObjectType;

                // Empty object type consider to be the table.
                // It should be empty only for the SQL queries.
                if (string.IsNullOrEmpty(objectType))
                {
                    return 0;
                }

                switch (objectType.ToLowerInvariant())
                {
                    case "tabledata":
                    case "table":
                        return 0;

                    case "form":
                        return 2;

                    case "report":
                        return 3;

                    case "dataport":
                        return 4;

                    case "codeunit":
                        return 5;

                    case "xmlport":
                        return 6;

                    case "menusuite":
                        return 7;

                    case "page":
                        return 8;

                    case "query":
                        return 9;

                    case "system":
                        return 10;

                    case "FieldNumber":
                        return 11;

                    case "pageextension":
                        return 14;

                    case "tableextension":
                        return 15;

                    case "enum":
                        return 16;

                    case "enumextension":
                        return 17;

                    case "profile":
                        return 18;

                    case "profileextension":
                        return 19;

                    case "permissionset":
                        return 20;

                    case "permissionsetextension":
                        return 21;

                    case "reportextension":
                        return 22;

                    default:
                        throw new InvalidOperationException($"Invalid object type: '{objectType}'.");
                }
            }
        }

        /// <summary>
        /// Gets the call tree' current current statement's hit count.
        /// </summary>
        public int CallTreeCurrentStatementHitCount
        {
            get
            {
                return this.callTree.Current.HitCount;
            }
        }

        /// <summary>
        /// Starts ETW profiling.
        /// </summary>
        /// <param name="sessionId">The session unique identifier.</param>
        /// <param name="threshold">The filter value in milliseconds. Values greater then this will only be shown.</param>
        public void Start(int sessionId, int threshold = 0)
        {
            this.dynamicProfilerEventProcessor = new DynamicProfilerEventProcessor(sessionId, threshold);

            this.dynamicProfilerEventProcessor.Start();
        }

        /// <summary>
        /// Stops profiling and aggregates the events
        /// </summary>
        public void Stop()
        {
            this.dynamicProfilerEventProcessor.Stop();

            this.callTree = this.dynamicProfilerEventProcessor.FlattenCallTree().GetEnumerator();

            this.maxRelativeTimeStamp = this.dynamicProfilerEventProcessor.MaxRelativeTimeStamp();
        }

        /// <summary>
        /// Analyzes events from the ETL file and aggregates events from the multiple sessions.
        /// </summary>
        /// <param name="etlFilePath">ETL file to be analyzed.</param>
        /// <param name="threshold">The filter value in milliseconds. Values greater then this will only be shown.</param>
        public void AnalyzeETLFile(string etlFilePath, int threshold = 0)
        {
            if (this.dynamicProfilerEventProcessor != null)
            {
                this.dynamicProfilerEventProcessor.Dispose();
                this.dynamicProfilerEventProcessor = null;
            }

            using (ProfilerEventEtlFileProcessor profilerEventEtlFileProcessor = new ProfilerEventEtlFileProcessor(etlFilePath, threshold))
            {
                profilerEventEtlFileProcessor.ProcessEtlFile();

                this.callTree = profilerEventEtlFileProcessor.FlattenCallTree().GetEnumerator();

                this.maxRelativeTimeStamp = profilerEventEtlFileProcessor.MaxRelativeTimeStamp();
            }
        }

        /// <summary>
        /// Calls the tree move next.
        /// </summary>
        /// <returns></returns>
        public bool CallTreeMoveNext()
        {
            return this.callTree.MoveNext();
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Disposes the object.
        /// </summary>
        /// <param name="disposing">True if managed resources should be disposed; otherwise, false.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (this.dynamicProfilerEventProcessor != null)
                {
                    this.dynamicProfilerEventProcessor.Dispose();
                }

                this.callTree = null;
            }
        }
    }
}
