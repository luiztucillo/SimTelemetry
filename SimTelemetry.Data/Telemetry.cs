﻿using System;
using System.Collections.Generic;
using System.Data;
using System.Data.OleDb;
using System.Diagnostics;
using System.Threading;
using SimTelemetry.Data.Logger;
using SimTelemetry.Data.Stats;
using SimTelemetry.Data.Track;
using SimTelemetry.Objects;
using SimTelemetry.Objects.Peripherals;
using Triton;
using Triton.Database;

namespace SimTelemetry.Data
{
    public sealed class Telemetry : ITelemetry
    {
        /// <summary>
        /// Single-ton Telemetry for general access everywhere.
        /// </summary>
        private static Telemetry _m = new Telemetry();
        public static Telemetry m { get { return _m; } }

        /// <summary>
        /// Track parser containing data about the current track.
        /// </summary>
        /// <remarks>Data is not directly available after Session_Start is fired.</remarks>
        public ITrackParser Track { get; set; }

        /// <summary>
        /// Data sent to hardware devices. Currently only used for exclusive peripherals.
        /// </summary>
        public IDevices Peripherals;

        /// <summary>
        /// Collection of simulators available in the plug-in directory. (As of debug it's the bin/ directory).
        /// </summary>
        public Simulators Sims { get; set; }

        /// <summary>
        /// Gets the running simulator. Returns null if not available.
        /// </summary>
        public ISimulator Sim { get { return Sims.GetRunning(); } }

        /// <summary>
        /// Returns true if a simulator is active. 
        /// </summary>
        public bool Active_Sim
        {
            get
            {
                if (Sims == null) // in case Sims is not initialised yet.
                    return false;
                else
                    return Sims.Available;
            }
        }

        /// <summary>
        /// Returns if any simulator has an active session.
        /// </summary>
        public bool Active_Session 
        { 
            get
            {
                if (Sims == null)
                    return false;
                else
                    return Active_Sim && Sim.Session.Active;
            }
        }

        /// <summary>
        /// Contains the state of each simulator and which simulator has an active session.
        /// This dictionary is used for comparison and firing events sim_start, sim_stop, session_start, session_stop.
        /// </summary>
        private Dictionary<string, Telemetry_SimState> Simulator_StateCollection = new Dictionary<string, Telemetry_SimState>();
        
        /// <summary>
        /// Thread for polling the status of all simulators and firing event whenever once has changed.
        /// </summary>
        private Thread Simulator_StatePollerThread;
        
        /// <summary>
        /// Data logger that always runs in the background collecting and storing telemetry data. 
        /// Is in co-junction with TelemetryStats which keeps track of driving statistics.
        /// </summary>
        private TelemetryLogger Logger;
        
        /// <summary>
        /// Class collecting driving stats, storing them into the log database and providing methods for
        /// searching and analyzing data.
        /// </summary>
        public TelemetryStats Stats { get; set; }

        #region Events
        /// <summary>
        /// Event fired whenever a simulator process starts. An object is passed containing the simulator instance.
        /// </summary>
        public event Signal Sim_Start;

        /// <summary>
        /// Event fired whenever a simulator process stops. An object is passed containing the simulator instance.
        /// </summary>
        public event Signal Sim_Stop;

        /// <summary>
        /// Event fired whenever a session starts. Note that the track is not yet available when this event fires;
        /// this is accessable after Track_Loaded is fired.
        /// </summary>
        public event Signal Session_Start;

        /// <summary>
        /// Event fired whenever a session stops.
        /// </summary>
        public event Signal Session_Stop;

        /// <summary>
        /// Event fired after Session_Start and the SimTelemetry framework has loaded the track data.
        /// </summary>
        public event Signal Track_Loaded;

        /// <summary>
        /// Event fired whenever a driver enters the cockpit view. Some simulators may not support this.
        /// </summary>
        public event Signal Driving_Start;

        /// <summary>
        /// Event fired whenever a driver leaves the cockpit view. Some simulators may not support this.
        /// </summary>
        public event Signal Driving_Stop;
        #endregion

        /// <summary>
        /// This creates a new OleDB connection to the SimTelemetry access database. This function is called
        /// from the Triton Database pool allowing simultanous access from various threads to the database.
        /// </summary>
        /// <returns>New database connection</returns>
        /// <permission cref="Triton.Database">Only used by Triton.Database namespace.</permission>
        private IDbConnection GetConnection()
        {
            OleDbConnection con = new OleDbConnection("Provider=Microsoft.ACE.OLEDB.12.0;Data source=Laptimes.accdb");
            return con;
        }

        /// <summary>
        /// Initialize telemetry. The main program will have to call Run() seperately.
        /// </summary>
        public Telemetry()
        {
            // Initialize the database with a maximum of 4 connections.
            DatabaseOleDbConnectionPool.MaxConnections = 4;
            DatabaseOleDbConnectionPool.Boot(GetConnection);

        }

        /// <summary>
        /// This method actually boots up the data frame-work. This method is called via the ThreadPool from Run().
        /// </summary>
        /// <param name="no">Do not pass any argument</param>
        public void Bootup(object no)
        {
            // Initialize simulator collection, data logger and stats collector.
            Sims = new Simulators();
            Logger = new TelemetryLogger(this);
            Stats = new TelemetryStats();

            Simulator_StatePollerThread = new Thread(Simulator_StatePoller);
            Simulator_StatePollerThread.Start();

            // When a session start load the track.
            Session_Start += Telemetry_Session_Start;

        }

        /// <summary>
        /// Whenever a session starts this function will trigger the track parser to load the new track file.
        /// The event Track_Loaded is fired with a 500ms delay to allow the track parser to complete.
        /// The session_start function is trigger from the Simulator_StatePoller, which runs seperately anyway.
        /// </summary>
        /// <param name="sender"></param>
        private void Telemetry_Session_Start(object sender)
        {
            // Start trackparser.
            //Wait 500ms because the track-parser may need some time to complete parsing the track.
            Track = new TrackParser(Sim.Session.GameDirectory, Sim.Session.GameData_TrackFile);
            Thread.Sleep(500);

            if (Track_Loaded != null)
                Track_Loaded(null);
        }

        /// <summary>
        /// Thread polling the status of all simulators.
        /// Will fire events Sim_Start/Stop, Session_Start/Stop.
        /// </summary>
        /// <remarks>For proper shutdown of this thread, call TritonBase.TriggerExit();</remarks>
        private void Simulator_StatePoller()
        {
            // Run whenver the TritonBase (Framework) is active.
            while (TritonBase.Active)
            {
                foreach (ISimulator sim in this.Sims.Sims)
                {
                    if (Simulator_StateCollection.ContainsKey(sim.ProcessName) == false)
                        Simulator_StateCollection.Add(sim.ProcessName, new Telemetry_SimState());

                    Telemetry_SimState state = Simulator_StateCollection[sim.ProcessName];

                    if (sim.Memory.Attached != state.Active) // Simulator events.
                    {
                        state.Active = sim.Memory.Attached;
                        if (sim.Memory.Attached)
                        {
                            Report_SimStart(sim);
                        }
                        else
                        {
                            // Also fire session if it was still active (unexpected close)
                            if (state.Session)
                                Report_SessionStop(sim);

                            Report_SimStop(sim);
                        }
                    }
                    else if (state.Active && sim.Session.Active != state.Session)// Session events.
                    {
                        state.Session = sim.Session.Active;
                        if (state.Session)
                        {
                            Report_SessionStart(sim);
                        }
                        else
                        {
                            Report_SessionStop(sim);
                        }
                    }
                    if(state.Active && sim.Drivers.Player.Driving != state.Driving)
                    {
                        state.Driving = sim.Drivers.Player.Driving;
                        if (state.Driving)
                        {
                            Report_DrivingStart(sim);
                        }
                        else
                        {
                            Report_DrivingStop(sim);
                        }
                    }

                    // Restore state.
                    Simulator_StateCollection[sim.ProcessName] = state;
                }

                // 50Hz
                Thread.Sleep(20);
            }
        }

        /// <summary>
        /// Fire Driving Start event.
        /// </summary>
        /// <param name="sim">Simulator with drive session started</param>
        private void Report_DrivingStart(ISimulator sim)
        {
            Debug.WriteLine("DrivingStart fired");
            if (Driving_Start != null)
                Driving_Start(sim);
        }

        /// <summary>
        /// Fire Driving Stop event.
        /// </summary>
        /// <param name="sim">Simulator with drive session stopped</param>
        private void Report_DrivingStop(ISimulator sim)
        {
            Debug.WriteLine("DrivingStop fired");
            if (Driving_Stop != null)
                Driving_Stop(sim);
        }

        /// <summary>
        /// Fire Sim Start event.
        /// </summary>
        /// <param name="me">Simulator started</param>
        internal void Report_SimStart(ISimulator me)
        {
            Debug.WriteLine("SimStart fired");
            if (Sim_Start != null)
                Sim_Start(me);
        }

        /// <summary>
        /// Fire Sim Stop event.
        /// </summary>
        /// <param name="me">Simulator stopped</param>
        internal void Report_SimStop(ISimulator me)
        {
            Debug.WriteLine("SimStop fired");
            if (Sim_Stop != null)
                Sim_Stop(me);
        }

        /// <summary>
        /// Fire Session Start event.
        /// </summary>
        /// <param name="me">Simulator of which session started</param>
        internal void Report_SessionStart(ISimulator me)
        {
            Debug.WriteLine("SessionStart fired");
            if (Session_Start != null)
                Session_Start(me);
        }

        /// <summary>
        /// Fire Session Stop event.
        /// </summary>
        /// <param name="me">Simulator of which session stopped</param>
        internal void Report_SessionStop(ISimulator me)
        {
            Debug.WriteLine("SessionStop fired");
            if (Session_Stop != null)
                Session_Stop(me);
        }

        /// <summary>
        /// Load new track. Specify location of gamedirectory and file.
        /// </summary>
        /// <param name="gamedir">Absolute path to gamedirectory.</param>
        /// <param name="track">Relative path from gamedirectory to track file.</param>
        public void Track_Load(string gamedir, string track)
        {
            Track = new TrackParser(gamedir, track);
        }

        /// <summary>
        /// Unloads track.
        /// </summary>
        public void Track_Unload()
        {
            Track = null;
        }

        /// <summary>
        /// Runs the telemetry network. The boot-up code is called via ThreadPool
        /// </summary>
        public void Run()
        {
            //Peripherals = new Devices();
            ThreadPool.QueueUserWorkItem(new WaitCallback(Bootup));
        }
    }
}