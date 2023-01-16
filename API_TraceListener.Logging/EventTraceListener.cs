using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq;
using System.Data.SqlClient;
namespace API_TraceListener.Logging
{
    /// <summary>
    /// TraceListener class that logs events into a SQL database.
    /// Only events are logged, other trace writes are ignored.
    /// </summary>
    public class EventTraceListener: TraceListener
    {
        private const string tableNameAttribute = "tableName";

        private static readonly object bufferLocker = new object();

        private string connectionString = null;
        private string tableName = null;
        private ConcurrentQueue<LogDetails> buffer = null;

        #region implemented TraceListener members

        /// <summary>
        /// Creates a new instance of the EventTraceListener class.
        /// </summary>
        public EventTraceListener()
        {
            this.connectionString = null;
            Initialize();
        }

        /// <summary>
        /// Creates a new instance of the EventTraceListener class.
        /// </summary>
        /// <param name="connectionString">Connection string for the database to write trace messages to</param>
        public EventTraceListener(string connectionString)
            : base()
        {
            this.connectionString = connectionString;
            Initialize();
        }

        /// <summary>
        /// Initializes a new instance of the EventTraceListener class.
        /// </summary>
        private void Initialize()
        {
            this.buffer = new ConcurrentQueue<LogDetails>();
        }

        /// <summary>
        /// Gets or sets the name of the database table where log entries are written to.
        /// </summary>
        private string TableName
        {
            get
            {
                if (this.Attributes.ContainsKey(tableNameAttribute))
                {
                    return this.Attributes[tableNameAttribute];
                }
                else
                {
                    return null;
                }
            }
            set
            {
                this.tableName = value;
            }
        }

        /// <summary>
        /// Closes the output stream so it no longer receives tracing or debugging output.
        /// </summary>
        /// <remarks>
        /// Calling a Fail, TraceData, TraceEvent, TraceTransfer, Write or WriteLine method after calling Close automatically reopens the listener.
        /// </remarks>
        public override void Close()
        {
            WriteToEventLog();
        }

        /// <summary>
        /// Emits an error message to the listener.
        /// </summary>
        /// <param name="message">A message to emit</param>
        public override void Fail(string message)
        { }

        /// <summary>
        /// Emits an error message and a detailed error message to the listener.
        /// </summary>
        /// <param name="message">A message to emit</param>
        /// <param name="detailMessage">A detailed message to emit</param>
        public override void Fail(string message, string detailMessage)
        {
            string combinedMessage = string.Join("\n", new object[] { message, detailMessage });
            Fail(combinedMessage);
        }

        /// <summary>
        /// Flushes the output buffer.
        /// </summary>
        public override void Flush()
        {
            WriteToEventLog();
        }

        /// <summary>
        /// Gets the custom attributes supported by the trace listener.
        /// </summary>
        /// <returns>A string array naming the custom attributes supported by the trace listener, or null if there are no custom attributes.</returns>
        protected override string[] GetSupportedAttributes()
        {
            string[] baseAttributes = base.GetSupportedAttributes();
            string[] eventLogAttributes = new string[] { tableNameAttribute };
            if (baseAttributes == null)
            {
                return eventLogAttributes;
            }
            else
            {
                return baseAttributes.Union(eventLogAttributes).ToArray();
            }
        }

        /// <summary>
        /// Writes trace information, a data object and event information to the listener specific output.
        /// </summary>
        /// <param name="eventCache">A TraceEventCache object that contains the current process ID, thread ID, and stack trace information.</param>
        /// <param name="source">A name used to identify the output, typically the name of the application that generated the trace event.</param>
        /// <param name="eventType">One of the TraceEventType values specifying the type of event that has caused the trace.</param>
        /// <param name="id">A numeric identifier for the event.</param>
        /// <param name="data">The trace data to emit.</param>
        public override void TraceData(TraceEventCache eventCache, string source, TraceEventType eventType, int id, object data)
        {
            string message = (data == null) ? null : data.ToString();
            TraceEvent(eventCache, source, eventType, id, message);
        }

        /// <summary>
        /// Writes trace information, an array of data objects and event information to the listener specific output.
        /// </summary>
        /// <param name="eventCache">A TraceEventCache object that contains the current process ID, thread ID, and stack trace information.</param>
        /// <param name="source">A name used to identify the output, typically the name of the application that generated the trace event.</param>
        /// <param name="eventType">One of the TraceEventType values specifying the type of event that has caused the trace.</param>
        /// <param name="id">A numeric identifier for the event.</param>
        /// <param name="data">An array of objects to emit as data.</param>
        public override void TraceData(TraceEventCache eventCache, string source, TraceEventType eventType, int id, params object[] data)
        {
            string message = string.Join("\n", data);
            TraceEvent(eventCache, source, eventType, id, message);
        }

        /// <summary>
        /// Writes trace and event information to the listener specific output.
        /// </summary>
        /// <param name="eventCache">A TraceEventCache object that contains the current process ID, thread ID, and stack trace information.</param>
        /// <param name="source">A name used to identify the output, typically the name of the application that generated the trace event.</param>
        /// <param name="eventType">One of the TraceEventType values specifying the type of event that has caused the trace.</param>
        /// <param name="id">A numeric identifier for the event.</param>
        public override void TraceEvent(TraceEventCache eventCache, string source, TraceEventType eventType, int id)
        {
            TraceEvent(eventCache, source, eventType, id, "no info");
        }

        /// <summary>
        /// Writes trace information, a formatted array of objects and event information to the listener specific output.
        /// </summary>
        /// <param name="eventCache">A TraceEventCache object that contains the current process ID, thread ID, and stack trace information.</param>
        /// <param name="source">A name used to identify the output, typically the name of the application that generated the trace event.</param>
        /// <param name="eventType">One of the TraceEventType values specifying the type of event that has caused the trace.</param>
        /// <param name="id">A numeric identifier for the event.</param>
        /// <param name="format">A format string that contains zero or more format items, which correspond to objects in the args array.</param>
        /// <param name="args">An object array containing zero or more objects to format.</param>
        public override void TraceEvent(TraceEventCache eventCache, string source, TraceEventType eventType, int id, string format, params object[] args)
        {
            string message = string.Format(format, args);
            TraceEvent(eventCache, source, eventType, id, message);
        }

        /// <summary>
        /// Writes trace information, a message, and event information to the listener specific output.
        /// </summary>
        /// <param name="eventCache">A TraceEventCache object that contains the current process ID, thread ID, and stack trace information.</param>
        /// <param name="source">A name used to identify the output, typically the name of the application that generated the trace event.</param>
        /// <param name="eventType">One of the TraceEventType values specifying the type of event that has caused the trace.</param>
        /// <param name="id">A numeric identifier for the event.</param>
        /// <param name="message">A message to write.</param>
        public override void TraceEvent(TraceEventCache eventCache, string source, TraceEventType eventType, int id, string message)
        {
            int logLevel = ToLogLevel(eventType);
            LogDetails logEntry = new LogDetails(source, logLevel, id, message);
            this.buffer.Enqueue(logEntry);
        }

        #endregion

        #region unimplemented TraceListener members

        /// <summary>
        /// Writes trace information, a message, a related activity identity and event information to the listener specific output.
        /// </summary>
        /// <param name="eventCache">A TraceEventCache object that contains the current process ID, thread ID, and stack trace information.</param>
        /// <param name="source">A name used to identify the output, typically the name of the application that generated the trace event.</param>
        /// <param name="id">A numeric identifier for the event.</param>
        /// <param name="message">A message to write.</param>
        /// <param name="relatedActivityId">A Guid object identifying a related activity.</param>
        public override void TraceTransfer(TraceEventCache eventCache, string source, int id, string message, System.Guid relatedActivityId)
        { }

        /// <summary>
        /// Writes the value of the object's ToString method to the listener you create when you implement the TraceListener class.
        /// </summary>
        /// <param name="o">An Object whose string representation you want to write.</param>
        public override void Write(object o)
        {
            string message = (o == null) ? null : o.ToString();
            Write(message);
        }

        /// <summary>
        /// Writes a category name and the value of the object's ToString method to the listener you create when you implement the TraceListener class.
        /// </summary>
        /// <param name="o">An Object whose string representation you want to write.</param>
        /// <param name="category">A category name used to organize the output.</param>
        public override void Write(object o, string category)
        {
            string message = (o == null) ? null : o.ToString();
            Write(message, category);
        }

        /// <summary>
        /// Writes the specified message to the listener you create in the derived class.
        /// </summary>
        /// <param name="message">A message to write</param>
        public override void Write(string message)
        { }

        /// Writes a category name and a message to the listener you create when you implement the TraceListener class.
        /// </summary>
        /// <param name="message">A message to write</param>
        /// <param name="category">A category name used to organize the output.</param>
        public override void Write(string message, string category)
        {
            LogDetails logEntry = new LogDetails(category, 4, null, message);
            this.buffer.Enqueue(logEntry);
        }

        /// <summary>
        /// Writes the value of the object's ToString method to the listener you create when you implement the TraceListener class.
        /// </summary>
        /// <param name="o">An Object whose string representation you want to write.</param>
        public override void WriteLine(object o)
        {
            string message = (o == null) ? null : o.ToString();
            WriteLine(message);
        }

        /// <summary>
        /// Writes a category name and the value of the object's ToString method to the listener you create when you implement the TraceListener class.
        /// </summary>
        /// <param name="o">An Object whose string representation you want to write.</param>
        /// <param name="category">A category name used to organize the output.</param>
        public override void WriteLine(object o, string category)
        {
            string message = (o == null) ? null : o.ToString();
            WriteLine(message, category);
        }

        /// <summary>
        /// Writes the specified message to the listener you create in the derived class.
        /// </summary>
        /// <param name="message">A message to write</param>
        public override void WriteLine(string message)
        {
            Write(message);
        }

        /// <summary>
        /// Writes a category name and a message to the listener you create when you implement the TraceListener class.
        /// </summary>
        /// <param name="message">A message to write</param>
        /// <param name="category">A category name used to organize the output.</param>
        public override void WriteLine(string message, string category)
        {
            Write(message, category);
        }

        #endregion

        #region IDisposable members

        /// <summary>
        /// Releases all resources.
        /// </summary>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (this.buffer != null && this.buffer.Count > 0)
                {
                    Flush();
                }
                base.Dispose(disposing);
            }
        }

        #endregion

        /// <summary>
        /// Writes all buffered log entries to the database.
        /// </summary>
        private void WriteToEventLog()
        {
            lock (bufferLocker)
            {
                SqlConnection connection = new SqlConnection(this.connectionString);
                connection.Open();
                try
                {
                    while (this.buffer.Count > 0)
                    {
                        LogDetails logEntry = null;
                        if (this.buffer.TryDequeue(out logEntry))
                        {
                            WriteToEventLog(connection, this.TableName, logEntry);
                        }
                    }
                }
                finally
                {
                    connection.Close();
                }
            }
        }

        /// <summary>
        /// Writes a record to the EventLog.
        /// </summary>
        /// <param name="connection">Connection to the database</param>
        /// <param name="tableName">Name of the database table where to insert the log record</param>
        /// <param name="logEntry">Details of log record to insert</param>
        private static void WriteToEventLog(SqlConnection connection, string tableName, LogDetails logEntry)
        {
            // Check input
            if (connection == null)
            {
                throw new System.ArgumentNullException("connection");
            }
            if (connection.State != System.Data.ConnectionState.Open)
            {
                throw new System.ArgumentException("Database connection is not open", "connection");
            }
            if (logEntry == null)
            {
                throw new System.ArgumentNullException("logEntry");
            }
            if (string.IsNullOrEmpty(logEntry.source))
            {
                throw new System.ArgumentNullException("logEntry", "Source is null or empty");
            }
            if (string.IsNullOrEmpty(logEntry.eventDetails))
            {
                throw new System.ArgumentNullException("LogEntry", "EventDetails is null or empty");
            }
            // Build SQL statement
            string sqlStr =
                "insert into [" + tableName + "]"
              + " ([Source],[Level],[Timestamp],[EventID],[EventDetails])"
              + " values (@Source,@Level,getdate(),@EventID,@EventDetails)";
            SqlCommand sqlCommand = new SqlCommand(sqlStr, connection);
            SqlParameter paramSource = new SqlParameter("@Source", System.Data.SqlDbType.NVarChar, 255);
            paramSource.Value = logEntry.source;
            sqlCommand.Parameters.Add(paramSource);
            SqlParameter paramLevel = new SqlParameter("@Level", System.Data.SqlDbType.Int);
            paramLevel.Value = logEntry.level;
            sqlCommand.Parameters.Add(paramLevel);
            SqlParameter paramEventID = new SqlParameter("@EventID", System.Data.SqlDbType.Int);
            //paramEventID.Value = (logEntry.eventID == null) ? System.DBNull.Value : logEntry.eventID.Value;
            if (logEntry.eventID == null)
            {
                paramEventID.Value = System.DBNull.Value;
            }
            else
            {
                paramEventID.Value = logEntry.eventID.Value;
            }
            sqlCommand.Parameters.Add(paramEventID);
            SqlParameter paramEventDetails = new SqlParameter("@EventDetails", System.Data.SqlDbType.NVarChar);
            paramEventDetails.Value = logEntry.eventDetails;
            sqlCommand.Parameters.Add(paramEventDetails);
            // Execute SQL statement
            sqlCommand.ExecuteNonQuery();
        }

        /// <summary>
        /// Converts a TraceEventType value to a log level that fits the EventLog database.
        /// </summary>
        /// <param name="traceEventType">TraceEventType value to convert</param>
        /// <returns>Log level</returns>
        private static int ToLogLevel(TraceEventType traceEventType)
        {
            switch (traceEventType)
            {
                case TraceEventType.Critical:
                    return 1;
                case TraceEventType.Error:
                    return 2;
                case TraceEventType.Warning:
                    return 3;
                case TraceEventType.Information:
                    return 4;
                case TraceEventType.Verbose:
                    return 5;
                case TraceEventType.Resume:
                case TraceEventType.Start:
                case TraceEventType.Stop:
                case TraceEventType.Suspend:
                case TraceEventType.Transfer:
                    return 0;
                default:
                    throw new System.ArgumentOutOfRangeException("Unknown TraceEventType: " + traceEventType);
            }
        }
    }

    internal class LogDetails
    {
        public LogDetails(string source, int level, int? eventID, string eventDetails)
        {
            this.source = source;
            this.level = level;
            this.eventID = eventID;
            this.eventDetails = eventDetails;
        }

        public string source { get; set; }
        public int level { get; set; }
        public int? eventID { get; set; }
        public string eventDetails { get; set; }
    }
}