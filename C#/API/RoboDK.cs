#region Namespaces

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Text;
using Microsoft.Win32;
using RoboDK.API.Exceptions;
using RoboDK.API.Model;

#endregion

namespace RoboDK.API
{
    /// <summary>
    ///     This class is the link to allows to create macros and automate Robodk.
    ///     Any interaction is made through \"items\" (Item() objects). An item is an object in the
    ///     robodk tree (it can be either a robot, an object, a tool, a frame, a program, ...).
    /// </summary>
    public class RoboDK : IRoboDK, IDisposable
    {
        #region Constants

        // Station parameters request
        public const string PATH_OPENSTATION = "PATH_OPENSTATION";
        public const string FILE_OPENSTATION = "FILE_OPENSTATION";
        public const string PATH_DESKTOP = "PATH_DESKTOP";

        #endregion

        #region Fields

        private bool _disposed;

        private Socket _COM; // tcpip com

        #endregion

        #region Constructors

        //%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%   

        /// <summary>
        ///     Creates a link with RoboDK
        /// </summary>
        /// <param name="robodk_ip"></param>
        /// <param name="start_hidden"></param>
        /// <param name="com_port"></param>
        public RoboDK(string robodk_ip = "localhost", bool start_hidden = false, int com_port = -1, string args = "",
            string path = "")
        {
            //A connection is attempted upon creation of the object"""
            if (robodk_ip != "")
                IP = robodk_ip;
            START_HIDDEN = start_hidden;
            if (com_port > 0)
            {
                PORT_FORCED = com_port;
                PORT_START = com_port;
                PORT_END = com_port;
            }

            if (path != "")
                ApplicationDir = path;
            if (args != "")
                ARGUMENTS = args;
            Connect();
        }

        #endregion

        #region Properties

        /// <summary>
        ///     timeout for communication, in miliseconds
        /// </summary>
        internal int TIMEOUT { get; private set; } = 10 * 1000;

        /// <summary>
        ///     arguments to provide to RoboDK on startup
        /// </summary>
        private string ARGUMENTS { get; } = "";

        /// <summary>
        ///     checks that provided items exist in memory
        /// </summary>
        private int SAFE_MODE { get; set; } = 1;

        /// <summary>
        ///     if AUTO_UPDATE is zero, the scene is rendered after every function call
        /// </summary>
        private int AUTO_UPDATE { get; set; }

        /// <summary>
        ///     IP address of the simulator (localhost if it is the same computer),
        ///     otherwise, use RL = Robolink('yourip') to set to a different IP
        /// </summary>
        private string IP { get; } = "localhost";

        /// <summary>
        ///     port to start looking for app connection
        /// </summary>
        private int PORT_START { get; } = 20500;

        /// <summary>
        ///     port to stop looking for app connection
        /// </summary>
        private int PORT_END { get; } = 20500;

        /// <summary>
        ///     forces to start hidden. ShowRoboDK must be used to show the window
        /// </summary>
        private bool START_HIDDEN { get; }

        /// <summary>
        ///     port where connection succeeded
        /// </summary>
        private int PORT { get; set; } = -1;

        /// <summary>
        ///     port to force RoboDK to start listening
        /// </summary>
        private int PORT_FORCED { get; } = -1;

        public int ReceiveTimeout
        {
            get { return _COM.ReceiveTimeout; }
            set { _COM.ReceiveTimeout = value; }
        }

        public Process Process { get; private set; } // pointer to the process

        public string ApplicationDir { get; private set; } =
            ""; // file path to the robodk program (executable), typically C:/RoboDK/bin/RoboDK.exe. Leave empty to use the registry key: HKEY_LOCAL_MACHINE\SOFTWARE\RoboDK

        #endregion

        #region Public Methods

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        ///     Checks if the object is currently linked to RoboDK
        /// </summary>
        /// <returns></returns>
        public bool Connected()
        {
            //return _socket.Connected;//does not work well
            if (_COM == null)
                return false;
            var part1 = _COM.Poll(1000, SelectMode.SelectRead);
            var part2 = _COM.Available == 0;
            if (part1 && part2)
                return false;
            return true;
        }

        /// <summary>
        ///     Disconnect from the RoboDK API. This flushes any pending program generation.
        /// </summary>
        public void Disconnect()
        {
            if (_COM != null && _COM.Connected)
                _COM.Disconnect(false);
        }

        /// <summary>
        ///     Starts the link with RoboDK (automatic upon creation of the object)
        /// </summary>
        /// <returns>True if connected; False otherwise</returns>
        public bool Connect()
        {
            //Establishes a connection with robodk. robodk must be running, otherwise, the variable APPLICATION_DIR must be set properly.
            var connected = false;
            int port;
            for (var i = 0; i < 2; i++)
            {
                for (port = PORT_START; port <= PORT_END; port++)
                {
                    _COM = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.IP);
                    //_socket = new Socket(SocketType.Stream, ProtocolType.IPv4);
                    _COM.SendTimeout = 1000;
                    _COM.ReceiveTimeout = 1000;
                    try
                    {
                        _COM.Connect(IP, port);
                        connected = is_connected();
                        if (connected)
                        {
                            _COM.SendTimeout = TIMEOUT;
                            _COM.ReceiveTimeout = TIMEOUT;
                            break;
                        }
                    }
                    catch (Exception e)
                    {
                        var s = e.Message;
                        //connected = false;
                    }
                }

                if (connected)
                {
                    PORT = port;
                    break;
                }

                if (IP != "localhost")
                    break;
                var arguments = "";
                if (PORT_FORCED > 0)
                    arguments = "/PORT=" + PORT_FORCED + " " + arguments;
                if (START_HIDDEN)
                    arguments = "/NOSPLASH /NOSHOW /HIDDEN " + arguments;
                if (ARGUMENTS != "")
                    arguments = arguments + ARGUMENTS;
                if (ApplicationDir == "")
                {
                    string install_path = null;

                    // retrieve install path from the registry:
                    /*RegistryKey localKey = RegistryKey.OpenBaseKey(Microsoft.Win32.RegistryHive.LocalMachine, RegistryView.Registry64);
                    localKey = localKey.OpenSubKey(@"SOFTWARE\RoboDK");
                    if (localKey != null)
                    {
                        install_path = localKey.GetValue("INSTDIR").ToString();
                        if (install_path != null)
                        {
                            APPLICATION_DIR = install_path + "\\bin\\RoboDK.exe";
                        }
                    }*/
                    var bits = IntPtr.Size * 8;
                    using (var hklm = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64))
                    using (var regKey = hklm.OpenSubKey(@"SOFTWARE\RoboDK"))
                    {
                        if (regKey != null)
                        {
                            // key now points to the 64-bit key
                            install_path = regKey.GetValue("INSTDIR").ToString();
                            if (!string.IsNullOrEmpty(install_path))
                                ApplicationDir = install_path + "\\bin\\RoboDK.exe";
                        }
                    }
                }

                if (ApplicationDir == "")
                    ApplicationDir = "C:/RoboDK/bin/RoboDK.exe";
                Process = Process.Start(ApplicationDir, arguments);
                // wait for the process to get started
                Process.WaitForInputIdle(10000);
            }

            if (connected && !Set_connection_params())
            {
                connected = false;
                Process = null;
            }

            return connected;
        }

        /////////////// Add More methods

        
        
        // %%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%
        // public methods
        /// <summary>
        /// Returns an item by its name. If there is no exact match it will return the last closest match.
        /// </summary>
        /// <param name="name">Item name</param>
        /// <param name="type">Filter by item type RoboDK.ITEM_TYPE_...</param>
        /// <returns></returns>
        public Item getItem(string name, ItemType itemType = ItemType.Any)
        {
            _check_connection();
            string command;
            if (itemType == ItemType.Any)
            {
                command = "G_Item";
                _send_Line(command);
                _send_Line(name);
            }
            else
            {
                command = "G_Item2";
                _send_Line(command);
                _send_Line(name);
                _send_Int((int) itemType);
            }
            Item item = _recv_Item();
            _check_status();
            return item;
        }
         // %%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%
        // public methods
        /// <summary>
        /// Returns an item by its name. If there is no exact match it will return the last closest match.
        /// </summary>
        /// <param name="name">Item name</param>
        /// <param name="type">Filter by item type RoboDK.ITEM_TYPE_...</param>
        /// <returns></returns>
        public Item GetItemByName(string name, ItemType itemType = ItemType.Any)
        {
            return getItem(name, itemType);
        }
        
        
        /// <summary>
        /// Returns a list of items (list of name or pointers) of all available items in the currently open station in robodk.
        /// Optionally, use a filter to return specific items (example: getItemListNames(filter = ITEM_CASE_ROBOT))
        /// </summary>
        /// <param name="filter">ITEM_TYPE</param>
        /// <returns></returns>
        public string[] getItemListNames(ItemType filter = ItemType.Any)
        {
            _check_connection();
            string command;
            if (filter < 0)
            {
                command = "G_List_Items";
                _send_Line(command);
            }
            else
            {
                command = "G_List_Items_Type";
                _send_Line(command);
                _send_Int((int)filter);
            }
            int numitems = _recv_Int();
            string[] listnames = new string[numitems];
            for (int i = 0; i < numitems; i++)
                listnames[i] = _recv_Line();
            _check_status();
            return listnames;
        }
        

        /// <summary>
        /// Returns a list of items (list of name or pointers) of all available items in the currently open station in robodk.
        /// Optionally, use a filter to return specific items (example: getItemListNames(filter = ITEM_CASE_ROBOT))
        /// </summary>
        /// <param name="filter">ITEM_TYPE</param>
        /// <returns></returns>
        public List<Item> GetItemList(ItemType filter = ItemType.Any)
        {
            _check_connection();
            string command;
            if (filter < 0)
            {
                command = "G_List_Items_ptr";
                _send_Line(command);
            }
            else
            {
                command = "G_List_Items_Type_ptr";
                _send_Line(command);
                _send_Int((int) filter);
            }
            int numitems = _recv_Int();
            var listitems = new List<Item>(numitems);
            for (int i = 0; i < numitems; i++)
            {
                listitems[i] = _recv_Item();
            }
            _check_status();
            return listitems;
        }

        /////// add more methods

        /// <summary>
        /// Shows a RoboDK popup to select one object from the open station.
        /// An item type can be specified to filter desired items. If no type is specified, all items are selectable.
        /// </summary>
        /// <param name="message">Message to pop up</param>
        /// <param name="itemtype">optionally filter by RoboDK.ITEM_TYPE_*</param>
        /// <returns></returns>
        public Item ItemUserPick(string message = "Pick one item", ItemType itemtype = ItemType.Any)
        {
            _check_connection();
            _send_Line("PickItem");
            _send_Line(message);
            _send_Int((int)itemtype);
            _COM.ReceiveTimeout = 3600 * 1000;
            Item item = _recv_Item();
            _COM.ReceiveTimeout = TIMEOUT;
            _check_status();
            return item;
        }

        /// <summary>
        /// Shows or raises the RoboDK window
        /// </summary>
        public void ShowRoboDK()
        {
            _check_connection();
            _send_Line("RAISE");
            _check_status();
        }

        /// <summary>
        /// Hides the RoboDK window
        /// </summary>
        public void HideRoboDK()
        {
            _check_connection();
            _send_Line("HIDE");
            _check_status();
        }

        /// <summary>
        ///     Closes RoboDK window and finishes RoboDK execution
        /// </summary>
        public void CloseRoboDK()
        {
            _check_connection();
            _send_Line("QUIT");
            _check_status();
            _COM.Disconnect(false);
            Process = null;
        }

        /// <summary>
        ///     Set the state of the RoboDK window
        /// </summary>
        /// <param name="windowState"></param>
        public void setWindowState(WindowState windowState = WindowState.Normal)
        {
            _check_connection();
            _send_Line("S_WindowState");
            _send_Int((int) windowState);
            _check_status();
        }

        /// <summary>
        /// Update the RoboDK flags. RoboDK flags allow defining how much access the user has to RoboDK features. Use FLAG_ROBODK_* variables to set one or more flags.
        /// </summary>
        /// <param name="flags">state of the window(FLAG_ROBODK_*)</param>
        public void setFlagsRoboDK(WindowFlags flags = WindowFlags.All)
        {
            _check_connection();
            _send_Line("S_RoboDK_Rights");
            _send_Int((int) flags);
            _check_status();
        }

        /// <summary>
        /// Update item flags. Item flags allow defining how much access the user has to item-specific features. Use FLAG_ITEM_* flags to set one or more flags.
        /// </summary>
        /// <param name="item"></param>
        /// <param name="flags"></param>
        public void setFlagsItem(Item item, int flags = FLAG_ITEM_ALL)
        {
            _check_connection();
            _send_Line("S_Item_Rights");
            _send_Item(item);
            _send_Int(flags);
            _check_status();
        }

        /// <summary>
        /// Retrieve current item flags. Item flags allow defining how much access the user has to item-specific features. Use FLAG_ITEM_* flags to set one or more flags.
        /// </summary>
        /// <param name="item"></param>
        /// <returns></returns>
        public int getFlagsItem(Item item)
        {
            _check_connection();
            _send_Line("S_Item_Rights");
            _send_Item(item);
            int flags = _recv_Int();
            _check_status();
            return flags;
        }

        /// <summary>
        /// Show a message in RoboDK (it can be blocking or non blocking in the status bar)
        /// </summary>
        /// <param name="message">Message to display</param>
        /// <param name="popup">Set to true to make the message blocking or set to false to make it non blocking</param>
        public void ShowMessage(string message, bool popup = true)
        {
            _check_connection();
            if (popup)
            {
                _send_Line("ShowMessage");
                _send_Line(message);
                _COM.ReceiveTimeout = 3600 * 1000;
                _check_status();
                _COM.ReceiveTimeout = TIMEOUT;
            }
            else
            {
                _send_Line("ShowMessageStatus");
                _send_Line(message);
                _check_status();
            }

        }

        /////////////// Add More methods
        /// <summary>
        ///     Loads a file and attaches it to parent. It can be any file supported by robodk.
        /// </summary>
        /// <param name="filename">absolute path of the file</param>
        /// <param name="parent">parent to attach. Leave empty for new stations or to load an object at the station root</param>
        /// <returns>Newly added object. Check with item.Valid() for a successful load</returns>
        public Item AddFile(string filename, Item parent = null)
        {
            if (!File.Exists(filename))
                throw new FileNotFoundException(filename);
            
            _check_connection();
            _send_Line("Add");
            _send_Line(filename);
            _send_Item(parent);
            Item newitem = _recv_Item();
            _check_status();
            return newitem;
        }

        /////////////// Add More methods

        /// <summary>
        /// Save an item to a file. If no item is provided, the open station is saved.
        /// </summary>
        /// <param name="filename">absolute path to save the file</param>
        /// <param name="itemsave">object or station to save. Leave empty to automatically save the current station.</param>
        public void Save(string filename, Item itemsave = null)
        {
            _check_connection();
            _send_Line("Save");
            _send_Line(filename);
            _send_Item(itemsave);
            _check_status();
        }

        /// <summary>
        /// Adds a shape provided triangle coordinates. Triangles must be provided as a list of vertices. A vertex normal can be provided optionally.
        /// </summary>
        /// <param name="triangle_points">List of vertices grouped by triangles (3xN or 6xN matrix, N must be multiple of 3 because vertices must be stacked by groups of 3)</param>
        /// <param name="add_to">item to attach the newly added geometry (optional). Leave empty to create a new object.</param>
        /// <param name="shape_override">Set to true to replace any other existing geometry</param>
        /// <returns></returns>
        public Item AddShape(Mat triangle_points, Item add_to = null, bool shape_override = false)
        {
            _check_connection();
            _send_Line("AddShape2");
            _send_Matrix2D(triangle_points);
            _send_Item(add_to);
            _send_Int(shape_override ? 1 : 0);
            Item newitem = _recv_Item();
            _check_status();
            return newitem;
        }


        /// <summary>
        /// Adds a curve provided point coordinates. The provided points must be a list of vertices. A vertex normal can be provided optionally.
        /// </summary>
        /// <param name="curve_points">matrix 3xN or 6xN -> N must be multiple of 3</param>
        /// <param name="reference_object">object to add the curve and/or project the curve to the surface</param>
        /// <param name="add_to_ref">If True, the curve will be added as part of the object in the RoboDK item tree (a reference object must be provided)</param>
        /// <param name="projection_type">Type of projection. For example: PROJECTION_ALONG_NORMAL_RECALC will project along the point normal and recalculate the normal vector on the surface projected.</param>
        /// <returns>added object/curve (null if failed)</returns>
        public Item AddCurve(Mat curve_points, Item reference_object = null, bool add_to_ref = false, int projection_type = PROJECTION_ALONG_NORMAL_RECALC)
        {
            _check_connection();
            _send_Line("AddWire");
            _send_Matrix2D(curve_points);
            _send_Item(reference_object);
            _send_Int(add_to_ref ? 1 : 0);
            _send_Int(projection_type);
            Item newitem = _recv_Item();
            _check_status();
            return newitem;
        }

        /// <summary>
        /// Projects a point given its coordinates. The provided points must be a list of [XYZ] coordinates. Optionally, a vertex normal can be provided [XYZijk].
        /// </summary>
        /// <param name="points">matrix 3xN or 6xN -> list of points to project</param>
        /// <param name="object_project">object to project</param>
        /// <param name="projection_type">Type of projection. For example: PROJECTION_ALONG_NORMAL_RECALC will project along the point normal and recalculate the normal vector on the surface projected.</param>
        /// <returns></returns>
        public Mat ProjectPoints(Mat points, Item object_project, int projection_type = PROJECTION_ALONG_NORMAL_RECALC)
        {
            _check_connection();
            _send_Line("ProjectPoints");
            _send_Matrix2D(points);
            _send_Item(object_project);
            _send_Int(projection_type);
            Mat projected_points = _recv_Matrix2D();
            _check_status();
            return projected_points;
        }

        /// <summary>
        /// Closes the current station without suggesting to save
        /// </summary>
        public void CloseStation()
        {
            _check_connection();
            _send_Line("Remove");
            _send_Item(new Item(this));
            _check_status();
        }

        /// <summary>
        /// Adds a new target that can be reached with a robot.
        /// </summary>
        /// <param name="name">name of the target</param>
        /// <param name="itemparent">parent to attach to (such as a frame)</param>
        /// <param name="itemrobot">main robot that will be used to go to self target</param>
        /// <returns>the new target created</returns>
        public Item AddTarget(string name, Item itemparent = null, Item itemrobot = null)
        {
            _check_connection();
            _send_Line("Add_TARGET");
            _send_Line(name);
            _send_Item(itemparent);
            _send_Item(itemrobot);
            Item newitem = _recv_Item();
            _check_status();
            return newitem;
        }

        /// <summary>
        /// Adds a new Frame that can be referenced by a robot.
        /// </summary>
        /// <param name="name">name of the reference frame</param>
        /// <param name="itemparent">parent to attach to (such as the robot base frame)</param>
        /// <returns>the new reference frame created</returns>
        public Item AddFrame(string name, Item itemparent = null)
        {
            _check_connection();
            _send_Line("Add_FRAME");
            _send_Line(name);
            _send_Item(itemparent);
            Item newitem = _recv_Item();
            _check_status();
            return newitem;
        }

        /// <summary>
        /// Adds a new Frame that can be referenced by a robot.
        /// </summary>
        /// <param name="name">name of the program</param>
        /// <param name="itemparent">robot that will be used</param>
        /// <returns>the new program created</returns>
        public Item AddProgram(string name, Item itemrobot = null)
        {
            _check_connection();
            _send_Line("Add_PROG");
            _send_Line(name);
            _send_Item(itemrobot);
            Item newitem = _recv_Item();
            _check_status();
            return newitem;
        }

        //%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%
        /// <summary>
        /// Adds a function call in the program output. RoboDK will handle the syntax when the code is generated for a specific robot. If the program exists it will also run the program in simulate mode.
        /// </summary>
        /// <param name="function_w_params">Function name with parameters (if any)</param>
        /// <returns></returns>
        public int RunProgram(string function_w_params)
        {
            return RunCode(function_w_params, true);
        }

        /// <summary>
        /// Adds code to run in the program output. If the program exists it will also run the program in simulate mode.
        /// </summary>
        /// <param name="code"></param>
        /// <param name="code_is_fcn_call"></param>
        /// <returns></returns>
        public int RunCode(string code, bool code_is_fcn_call = false)
        {
            _check_connection();
            _send_Line("RunCode");
            _send_Int(code_is_fcn_call ? 1 : 0);
            _send_Line(code);
            int prog_status = _recv_Int();
            _check_status();
            return prog_status;
        }

        /// <summary>
        /// Shows a message or a comment in the output robot program.
        /// </summary>
        /// <param name="message"></param>
        /// <param name="message_is_comment"></param>
        public void RunMessage(string message, bool message_is_comment = false)
        {
            _check_connection();
            _send_Line("RunMessage");
            _send_Int(message_is_comment ? 1 : 0);
            _send_Line(message);
            _check_status();
        }

        /// <summary>
        /// Renders the scene. This function turns off rendering unless always_render is set to true.
        /// </summary>
        /// <param name="always_render"></param>
        public void Render(bool always_render = false)
        {
            bool auto_render = !always_render;
            _check_connection();
            _send_Line("Render");
            _send_Int(auto_render ? 1 : 0);
            _check_status();
        }

        /// <summary>
        /// Returns (1/True) if object_inside is inside the object_parent
        /// </summary>
        /// <param name="object_inside"></param>
        /// <param name="object_parent"></param>
        /// <returns></returns>
        public bool IsInside(Item object_inside, Item object_parent)
        {
            _check_connection();
            _send_Line("IsInside");
            _send_Item(object_inside);
            _send_Item(object_parent);
            int inside = _recv_Int();
            _check_status();
            return inside > 0;
        }

        /// <summary>
        /// Set collision checking ON or OFF (COLLISION_OFF/COLLISION_OFF) according to the collision map. If collision check is activated it returns the number of pairs of objects that are currently in a collision state.
        /// </summary>
        /// <param name="check_state"></param>
        /// <returns>Number of pairs of objects in a collision state</returns>
        public int setCollisionActive(int check_state = COLLISION_ON)
        {
            _check_connection();
            _send_Line("Collision_SetState");
            _send_Int(check_state);
            int ncollisions = _recv_Int();
            _check_status();
            return ncollisions;
        }

        /// <summary>
        /// Set collision checking ON or OFF (COLLISION_ON/COLLISION_OFF) for a specific pair of objects. This allows altering the collision map for Collision checking. 
        /// Specify the link id for robots or moving mechanisms (id 0 is the base).
        /// </summary>
        /// <param name="check_state">Set to COLLISION_ON or COLLISION_OFF</param>
        /// <param name="item1">Item 1</param>
        /// <param name="item2">Item 2</param>
        /// <param name="id1">Joint id for Item 1 (if Item 1 is a robot or a mechanism)</param>
        /// <param name="id2">Joint id for Item 2 (if Item 2 is a robot or a mechanism)</param>
        /// <returns>Returns true if succeeded. Returns false if setting the pair failed (wrong id was provided)</returns>
        public bool setCollisionActivePair(int check_state, Item item1, Item item2, int id1 = 0, int id2 = 0)
        {
            _check_connection();
            _send_Line("Collision_SetPair");
            _send_Item(item1);
            _send_Item(item2);
            _send_Int(id1);
            _send_Int(id2);
            _send_Int(check_state);
            int success = _recv_Int();
            _check_status();
            return success > 0;
        }

        /// <summary>
        /// Returns the number of pairs of objects that are currently in a collision state.
        /// </summary>
        /// <returns></returns>
        public int Collisions()
        {
            _check_connection();
            _send_Line("Collisions");
            int ncollisions = _recv_Int();
            _check_status();
            return ncollisions;
        }

        /// <summary>
        /// Returns 1 if item1 and item2 collided. Otherwise returns 0.
        /// </summary>
        /// <param name="item1"></param>
        /// <param name="item2"></param>
        /// <returns></returns>
        public int Collision(Item item1, Item item2)
        {
            _check_connection();
            _send_Line("Collided");
            _send_Item(item1);
            _send_Item(item2);
            int ncollisions = _recv_Int();
            _check_status();
            return ncollisions;
        }

        /// <summary>
        /// Sets the current simulation speed. Set the speed to 1 for a real-time simulation. The slowest speed allowed is 0.001 times the real speed. Set to a high value (>100) for fast simulation results.
        /// </summary>
        /// <param name="speed"></param>
        public void setSimulationSpeed(double speed)
        {
            _check_connection();
            _send_Line("SimulateSpeed");
            _send_Int((int)(speed * 1000.0));
            _check_status();
        }

        /// <summary>
        /// Gets the current simulation speed. Set the speed to 1 for a real-time simulation.
        /// </summary>
        /// <returns></returns>
        public double SimulationSpeed()
        {
            _check_connection();
            _send_Line("GetSimulateSpeed");
            double speed = ((double)_recv_Int()) / 1000.0;
            _check_status();
            return speed;
        }
        /// <summary>
        /// Sets the behavior of the RoboDK API. By default, robodk shows the path simulation for movement instructions (run_mode=1=RUNMODE_SIMULATE).
        /// Setting the run_mode to RUNMODE_QUICKVALIDATE allows performing a quick check to see if the path is feasible.
        /// if robot.Connect() is used, RUNMODE_RUN_FROM_PC is selected automatically.
        /// </summary>
        /// <param name="run_mode">int = RUNMODE
        /// RUNMODE_SIMULATE=1        performs the simulation moving the robot (default)
        /// RUNMODE_QUICKVALIDATE=2   performs a quick check to validate the robot movements
        /// RUNMODE_MAKE_ROBOTPROG=3  makes the robot program
        /// RUNMODE_RUN_REAL=4        moves the real robot is it is connected</param>
        public void setRunMode(int run_mode = 1)
        {
            _check_connection();
            _send_Line("S_RunMode");
            _send_Int(run_mode);
            _check_status();
        }

        /// <summary>
        /// Returns the behavior of the RoboDK API. By default, robodk shows the path simulation for movement instructions (run_mode=1)
        /// </summary>
        /// <returns>int = RUNMODE
        /// RUNMODE_SIMULATE=1        performs the simulation moving the robot (default)
        /// RUNMODE_QUICKVALIDATE=2   performs a quick check to validate the robot movements
        /// RUNMODE_MAKE_ROBOTPROG=3  makes the robot program
        /// RUNMODE_RUN_REAL=4        moves the real robot is it is connected</returns>
        public int RunMode()
        {
            _check_connection();
            _send_Line("G_RunMode");
            int runmode = _recv_Int();
            _check_status();
            return runmode;
        }

        /// <summary>
        /// Gets all the user parameters from the open RoboDK station.
        /// The parameters can also be modified by right clicking the station and selecting "shared parameters"
        /// User parameters can be added or modified by the user
        /// </summary>
        /// <returns>list of pairs of strings as parameter-value (list of a list)</returns>
        public List<List<string>> getParams()
        {
            _check_connection();
            _send_Line("G_Params");
            List<List<string>> paramlist = new List<List<string>>();
            int nparam = _recv_Int();
            for (int i = 0; i < nparam; i++)
            {
                string param = _recv_Line();
                string value = _recv_Line();

                List<string> param_value = new List<string>();
                param_value.Add(param);
                param_value.Add(value);
                paramlist.Add(param_value);
            }
            _check_status();
            return paramlist;
        }

        /// <summary>
        /// Gets a global or a user parameter from the open RoboDK station.
        /// The parameters can also be modified by right clicking the station and selecting "shared parameters"
        /// Some available parameters:
        /// PATH_OPENSTATION = folder path of the current .stn file
        /// FILE_OPENSTATION = file path of the current .stn file
        /// PATH_DESKTOP = folder path of the user's folder
        /// Other parameters can be added or modified by the user
        /// </summary>
        /// <param name="param">RoboDK parameter</param>
        /// <returns>value</returns>
        public string getParam(string param)
        {
            _check_connection();
            _send_Line("G_Param");
            _send_Line(param);
            string value = _recv_Line();
            if (value.StartsWith("UNKNOWN "))
            {
                value = null;
            }
            _check_status();
            return value;
        }

        /// <summary>
        /// Sets a global parameter from the RoboDK station. If the parameters exists, it will be modified. If not, it will be added to the station.
        /// The parameters can also be modified by right clicking the station and selecting "shared parameters"
        /// </summary>
        /// <param name="param">RoboDK parameter</param>
        /// <param name="value">value</param>
        /// <returns></returns>
        public void setParam(string param, string value)
        {
            _check_connection();
            _send_Line("S_Param");
            _send_Line(param);
            _send_Line(value);
            _check_status();
        }


        /// <summary>
        /// Takes a laser tracker measurement with respect to its own reference frame. If an estimate point is provided, the laser tracker will first move to those coordinates. If search is True, the tracker will search for a target.
        /// </summary>
        /// <param name="estimate"></param>
        /// <param name="search">Returns the XYZ coordinates of the target (in mm). If the target was not found it retuns a null pointer.</param>
        /// <returns></returns>
        public double[] LaserTracker_Measure(double[] estimate, bool search = false)
        {
            _check_connection();
            _send_Line("MeasLT");
            _send_XYZ(estimate);
            _send_Int(search ? 1 : 0);
            double[] xyz = new double[3];
            _recv_XYZ(xyz);
            _check_status();
            if (xyz[0] * xyz[0] + xyz[1] * xyz[1] + xyz[2] * xyz[2] < 0.0001)
            {
                return null;
            }
            return xyz;
        }

        /// <summary>
        /// Takes a measurement with the C-Track stereocamera. It returns two poses, the base reference frame and the measured object reference frame.Status is 0 if measurement succeeded.
        /// </summary>
        /// <param name="pose1">Pose of the measurement reference</param>
        /// <param name="pose2">Pose of the tool measurement</param>
        /// <param name="npoints1">number of visible targets for the measurement pose</param>
        /// <param name="npoints2">number of visible targets for the tool pose</param>
        /// <param name="time">time stamp in milliseconds</param>
        /// <param name="status">Status is 0 if measurement succeeded</param>
        public void StereoCamera_Measure(out Mat pose1, out Mat pose2, out int npoints1, out int npoints2, out int time, out int status)
        {
            _check_connection();
            _send_Line("MeasPose");
            pose1 = _recv_Pose();
            pose2 = _recv_Pose();
            npoints1 = _recv_Int();
            npoints2 = _recv_Int();
            time = _recv_Int();
            status = _recv_Int();
            _check_status();
        }

        /// <summary>
        /// Checks the collision between a line and any objects in the station. The line is composed by 2 points.
        /// Returns the collided item. Use Item.Valid() to check if there was a valid collision.
        /// </summary>
        /// <param name="p1">start point of the line</param>
        /// <param name="p2">end point of the line</param>
        /// <param name="ref_abs">Reference of the two points with respect to the absolute station reference.</param>
        /// /// <param name="xyz_collision">Collided point.</param>
        public Item Collision_Line(double[] p1, double[] p2, Mat ref_abs = null, double[] xyz_collision = null)
        {
            double[] p1_abs = new double[3];
            double[] p2_abs = new double[3];

            if (ref_abs != null)
            {
                p1_abs = ref_abs * p1;
                p2_abs = ref_abs * p2;
            }
            else
            {
                p1_abs = p1;
                p2_abs = p2;
            }
            _check_connection();
            _send_Line("CollisionLine");
            _send_XYZ(p1_abs);
            _send_XYZ(p2_abs);
            Item itempicked = _recv_Item();
            if (xyz_collision != null)
            {
                _recv_XYZ(xyz_collision);
            }
            else
            {
                double[] xyz = new double[3];
                _recv_XYZ(xyz);
            }
            bool collision = itempicked.Valid();
            _check_status();
            return itempicked;
        }       

        /// <summary>
        /// Returns the current joints of a list of robots.
        /// </summary>
        /// <param name="robot_item_list">list of robot items</param>
        /// <returns>list of robot joints (double x nDOF)</returns>
        public double[][] Joints(Item[] robot_item_list)
        {
            _check_connection();
            _send_Line("G_ThetasList");
            int nrobs = robot_item_list.Length;
            _send_Int(nrobs);
            double[][] joints_list = new double[nrobs][];
            for (int i = 0; i < nrobs; i++)
            {
                _send_Item(robot_item_list[i]);
                joints_list[i] = _recv_Array();
            }
            _check_status();
            return joints_list;
        }

        /// <summary>
        /// Sets the current robot joints for a list of robot items and a list of a set of joints.
        /// </summary>
        /// <param name="robot_item_list">list of robot items</param>
        /// <param name="joints_list">list of robot joints (double x nDOF)</param>
        public void setJoints(Item[] robot_item_list, double[][] joints_list)
        {
            int nrobs = Math.Min(robot_item_list.Length, joints_list.Length);
            _check_connection();
            _send_Line("S_ThetasList");
            _send_Int(nrobs);
            for (int i = 0; i < nrobs; i++)
            {
                _send_Item(robot_item_list[i]);
                _send_Array(joints_list[i]);
            }
            _check_status();
        }

        /// <summary>
        /// Calibrate a tool (TCP) given a number of points or calibration joints. Important: If the robot is calibrated, provide joint values to maximize accuracy.
        /// </summary>
        /// <param name="poses_joints">matrix of poses in a given format or a list of joints</param>
        /// <param name="error_stats">stats[mean, standard deviation, max] - Output error stats summary</param>
        /// <param name="format">Euler format. Optionally, use JOINT_FORMAT and provide the robot.</param>
        /// <param name="algorithm">type of algorithm (by point, plane, ...)</param>
        /// <param name="robot">Robot used for calibration (if using joint values)</param>
        /// <returns>TCP as [x, y, z] - calculated TCP</returns>
        /// 
        public double[] CalibrateTool(Mat poses_joints, out double[] error_stats, int format = EULER_RX_RY_RZ, int algorithm = CALIBRATE_TCP_BY_POINT, Item robot = null)
        {
            _check_connection();
            _send_Line("CalibTCP2");
            _send_Matrix2D(poses_joints);
            _send_Int(format);
            _send_Int(algorithm);
            _send_Item(robot);
            double[] tcp = _recv_Array();
            error_stats = _recv_Array();
            Mat error_graph = _recv_Matrix2D();
            _check_status();
            return tcp;
            //errors = errors[:, 1].tolist()
        }

        /// <summary>
        /// Calibrate a Reference Frame given a list of points or joint values. Important: If the robot is calibrated, provide joint values to maximize accuracy.
        /// </summary>
        /// <param name="joints">points as a 3xN matrix or nDOFsxN) - List of points or a list of robot joints</param>
        /// <param name="method">type of algorithm(by point, plane, ...) CALIBRATE_FRAME_...</param>
        /// <param name="use_joints">use points or joint values. The robot item must be provided if joint values is used.</param>
        /// <param name="robot"></param>
        /// <returns></returns>
        public Mat CalibrateReference(Mat joints, int method = CALIBRATE_FRAME_3P_P1_ON_X, bool use_joints = false, Item robot = null)
        {
            _check_connection();
            _send_Line("CalibFrame");
            _send_Matrix2D(joints);
            _send_Int(use_joints ? -1 : 0);
            _send_Int(method);
            _send_Item(robot);
            Mat reference_pose = _recv_Pose();
            double[] error_stats = _recv_Array();
            _check_status();
            //errors = errors[:, 1].tolist()
            return reference_pose;
        }

        /// <summary>
        /// Defines the name of the program when the program is generated. It is also possible to specify the name of the post processor as well as the folder to save the program. 
        /// This method must be called before any program output is generated (before any robot movement or other instruction).
        /// </summary>
        /// <param name="progname">name of the program</param>
        /// <param name="defaultfolder">folder to save the program, leave empty to use the default program folder</param>
        /// <param name="postprocessor">name of the post processor (for a post processor in C:/RoboDK/Posts/Fanuc_post.py it is possible to provide "Fanuc_post.py" or simply "Fanuc_post")</param>
        /// <param name="robot">Robot to link</param>
        /// <returns></returns>
        public int ProgramStart(string progname, string defaultfolder = "", string postprocessor = "", Item robot = null)
        {
            _check_connection();
            _send_Line("ProgramStart");
            _send_Line(progname);
            _send_Line(defaultfolder);
            _send_Line(postprocessor);
            _send_Item(robot);
            int errors = _recv_Int();
            _check_status();
            return errors;
        }
        
        /// <summary>
        /// Set the pose of the wold reference frame with respect to the view (camera/screen)
        /// </summary>
        /// <param name="pose"></param>
        public void setViewPose(Mat pose)
        {
            _check_connection();
            _send_Line("S_ViewPose");
            _send_Pose(pose);
            _check_status();
        }

        /// <summary>
        /// Get the pose of the wold reference frame with respect to the view (camera/screen)
        /// </summary>
        /// <param name="pose"></param>
        public Mat ViewPose()
        {
            _check_connection();
            _send_Line("G_ViewPose");
            Mat pose = _recv_Pose();
            _check_status();
            return pose;
        }

        /// <summary>
        /// Gets the nominal robot parameters
        /// </summary>
        /// <param name="robot"></param>
        /// <param name="dhm"></param>
        /// <param name="pose_base"></param>
        /// <param name="pose_tool"></param>
        /// <returns></returns>
        public bool setRobotParams(Item robot, double[][] dhm, Mat pose_base, Mat pose_tool)
        {
            _check_connection();
            _send_Line("S_AbsAccParam");
            _send_Item(robot);
            Mat r2b = Mat.Identity4x4();
            _send_Pose(r2b);
            _send_Pose(pose_base);
            _send_Pose(pose_tool);
            int ndofs = dhm.Length;
            _send_Int(ndofs);
            for (int i = 0; i < ndofs; i++)
            {
                _send_Array(dhm[i]);
            }

            // for internal use only
            _send_Pose(pose_base);
            _send_Pose(pose_tool);
            _send_Int(ndofs);
            for (int i = 0; i < ndofs; i++)
            {
                _send_Array(dhm[i]);
            }
            // reserved
            _send_Array(null);
            _send_Array(null);
            _check_status();
            return true;
        }


        //------------------------------------------------------------------
        //----------------------- CAMERA VIEWS -----------------------------
        /// <summary>
        /// Open a simulated 2D camera view. Returns a handle pointer that can be used in case more than one simulated view is used.
        /// </summary>
        /// <param name="item">Reference frame or other object to attach the camera</param>
        /// <param name="cam_params">Camera parameters as a string. Refer to the documentation for more information.</param>
        /// <returns>Camera pointer/handle. Keep the handle if more than 1 simulated camera is used</returns>
        public UInt64 Cam2D_Add(Item item, string cam_params = "")
        {
            _check_connection();
            _send_Line("Cam2D_Add");
            _send_Item(item);
            _send_Line(cam_params);
            UInt64 ptr = _recv_Ptr();
            _check_status();
            return ptr;
        }

        /// <summary>
        /// Take a snapshot from a simulated camera view and save it to a file. Returns 1 if success, 0 otherwise.
        /// </summary>
        /// <param name="file_save_img">file path to save.Formats supported include PNG, JPEG, TIFF, ...</param>
        /// <param name="cam_handle">amera handle(pointer returned by Cam2D_Add)</param>
        /// <returns></returns>
        public bool Cam2D_Snapshot(string file_save_img, UInt64 cam_handle = 0) {
            _check_connection();
            _send_Line("Cam2D_Snapshot");
            _send_Ptr(cam_handle);
            _send_Line(file_save_img);
            int success = _recv_Int();
            _check_status();
            return success > 0;
        }

        /// <summary>
        /// Closes all camera windows or one specific camera if the camera handle is provided. Returns 1 if success, 0 otherwise.
        /// </summary>
        /// <param name="cam_handle">camera handle(pointer returned by Cam2D_Add). Leave to 0 to close all simulated views.</param>
        /// <returns></returns>
        public bool Cam2D_Close(UInt64 cam_handle = 0) {
            _check_connection();
            if (cam_handle == 0) {
                _send_Line("Cam2D_CloseAll");
            }
            else
            {
                _send_Line("Cam2D_Close");
                _send_Ptr(cam_handle);
            }
            int success = _recv_Int();
            _check_status();
            return success > 0;
        }

        /// <summary>
        /// Set the parameters of the simulated camera.
        /// </summary>
        /// <param name="cam_params">parameter settings according to the parameters supported by Cam2D_Add</param>
        /// <param name="cam_handle">camera handle (optional)</param>
        /// <returns></returns>
        public bool Cam2D_SetParams(string cam_params, UInt64 cam_handle = 0)
        {
            _check_connection();
            _send_Line("Cam2D_SetParams");
            _send_Ptr(cam_handle);
            _send_Line(cam_params);
            int success = _recv_Int();
            _check_status();
            return success > 0;
        }
        //-----------------------------------------------------------------------------------

        /// <summary>
        /// Returns the license string (as shown in the RoboDK main window)
        /// </summary>
        /// <returns></returns>
        public string License()
        {
            _check_connection();
            _send_Line("G_License");
            string license = _recv_Line();
            _check_status();
            return license;
        }

        /// <summary>
        /// Returns the list of items selected (it can be one or more items)
        /// </summary>
        /// <returns></returns>
        public List<Item> Selection()
        {
            _check_connection();
            _send_Line("G_Selection");
            int nitems = _recv_Int();
            List<Item> list_items = new List<Item>(nitems);
            for (int i = 0; i < nitems; i++)
            {
                list_items[i] = _recv_Item();
            }
            _check_status();
            return list_items;
        }
        

        public void AddTargetJ(Item pgm, string targetName, double[] joints, Item robotBase = null, Item robot = null)
        {
            var target = AddTarget(targetName, robotBase);
            if (target == null)
                throw new Exception($"Create target '{targetName}' failed.");
            target.setVisible(false);
            target.setAsJointTarget();
            target.setJoints(joints);
            if (robot != null)
                target.setRobot(robot);

            //target
            pgm.addMoveJ(target);
        }

        #endregion

        #region Protected Methods

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                    if (_COM != null)
                        _COM.Dispose();

                _disposed = true;
            }
        }

        #endregion

        //Returns 1 if connection is valid, returns 0 if connection is invalid
        bool is_connected()
        {
            return _COM != null && _COM.Connected;
        }

        //If we are not connected it will attempt a connection, if it fails, it will throw an error
        void _check_connection()
        {
            if (!is_connected() && !Connect())
            {
                throw new RdkException("Can't connect to RoboDK library");
            }
        }

        // checks the status of the connection
        int _check_status()
        {
            int status = _recv_Int();
            if (status > 0 && status < 10)
            {
                string strproblems;
                strproblems = "Unknown error";
                if (status == 1)
                {
                    strproblems = "Invalid item provided: The item identifier provided is not valid or it does not exist.";
                }
                else if (status == 2)
                {//output warning
                    strproblems = _recv_Line();
                    //print("WARNING: " + strproblems);
                    //#warn(strproblems)# does not show where is the problem...
                    return 0;
                }
                else if (status == 3)
                { // output error
                    strproblems = _recv_Line();
                    throw new RdkException(strproblems);
                }
                else if (status == 9)
                {
                    strproblems = "Invalid license. Contact us at: info@robodk.com";
                }
                //print(strproblems);
                throw new RdkException(strproblems); //raise Exception(strproblems)
            }
            else if (status == 0)
            {
                // everything is OK
                //status = status
            }
            else
            {
                throw new RdkException("Problems running function"); //raise Exception('Problems running function');
            }
            return status;
        }

        //Formats the color in a vector of size 4x1 and ranges [0,1]
        bool check_color(double[] color)
        {
            if (color.Length < 4)
            {
                throw new RdkException("Invalid color. A color must be a 4-size double array [r,g,b,a]"); //raise Exception('Problems running function');
                //return false;
            }
            return true;
        }

        //Sends a string of characters with a \\n
        void _send_Line(string line)
        {
            line = line.Replace('\n', ' ');// one new line at the end only!
            byte[] data = Encoding.UTF8.GetBytes(line + "\n");
            try
            {
                _COM.Send(data);
            }
            catch
            {
                throw new RdkException("Send line failed.");
            }
        }

        string _recv_Line()
        {
            //Receives a string. It reads until if finds LF (\\n)
            byte[] buffer = new byte[1];
            int bytesread = _COM.Receive(buffer, 1, SocketFlags.None);
            string line = "";
            while (bytesread > 0 && buffer[0] != '\n')
            {
                line = line + System.Text.Encoding.UTF8.GetString(buffer);
                bytesread = _COM.Receive(buffer, 1, SocketFlags.None);
            }
            return line;
        }

        //Sends an item pointer
        void _send_Item(Item item)
        {
            byte[] bytes;
            if (item == null)
            {
                bytes = BitConverter.GetBytes(((UInt64)0));
            }
            else
            {
                bytes = BitConverter.GetBytes((UInt64)item.get_item());
            }
            if (bytes.Length != 8)
            {
                throw new RdkException("RoboDK API error");
            }
            Array.Reverse(bytes);
            _COM.Send(bytes);
        }

        //Receives an item pointer
        Item _recv_Item()
        {
            byte[] buffer1 = new byte[8];
            byte[] buffer2 = new byte[4];
            int read1 = _COM.Receive(buffer1, 8, SocketFlags.None);
            int read2 = _COM.Receive(buffer2, 4, SocketFlags.None);
            if (read1 != 8 || read2 != 4)
            {
                return null;
            }
            Array.Reverse(buffer1);
            Array.Reverse(buffer2);
            UInt64 item = BitConverter.ToUInt64(buffer1, 0);
            //Console.WriteLine("Received item: " + item.ToString());
            Int32 type = BitConverter.ToInt32(buffer2, 0);
            return new Item(this, item, type);
        }
        //Sends an item pointer
        void _send_Ptr(UInt64 ptr = 0)
        {
            byte[] bytes = BitConverter.GetBytes(ptr);
            if (bytes.Length != 8)
            {
                throw new RdkException("RoboDK API error");
            }
            Array.Reverse(bytes);
            _COM.Send(bytes);
        }

        //Receives an item pointer
        UInt64 _recv_Ptr()
        {
            byte[] bytes = new byte[8];
            int read = _COM.Receive(bytes, 8, SocketFlags.None);
            if (read != 8)
            {
                return 0;
            }
            Array.Reverse(bytes);
            UInt64 ptr = BitConverter.ToUInt64(bytes, 0);
            return ptr;
        }

        void _send_Pose(Mat pose)
        {
            if (!pose.IsHomogeneous())
            {
                // warning!!
                return;
            }
            const int nvalues = 16;
            byte[] bytesarray = new byte[8 * nvalues];
            int cnt = 0;
            for (int j = 0; j < pose.Cols; j++)
            {
                for (int i = 0; i < pose.Rows; i++)
                {
                    byte[] onedouble = BitConverter.GetBytes((double)pose[i, j]);
                    Array.Reverse(onedouble);
                    Array.Copy(onedouble, 0, bytesarray, cnt * 8, 8);
                    cnt = cnt + 1;
                }
            }
            _COM.Send(bytesarray, 8 * nvalues, SocketFlags.None);
        }

        Mat _recv_Pose()
        {
            Mat pose = new Mat(4, 4);
            byte[] bytes = new byte[16 * 8];
            int nbytes = _COM.Receive(bytes, 16 * 8, SocketFlags.None);
            if (nbytes != 16 * 8)
            {
                throw new RdkException("Invalid pose sent"); //raise Exception('Problems running function');
            }
            int cnt = 0;
            for (int j = 0; j < pose.Cols; j++)
            {
                for (int i = 0; i < pose.Rows; i++)
                {
                    byte[] onedouble = new byte[8];
                    Array.Copy(bytes, cnt, onedouble, 0, 8);
                    Array.Reverse(onedouble);
                    pose[i, j] = BitConverter.ToDouble(onedouble, 0);
                    cnt = cnt + 8;
                }
            }
            return pose;
        }

        void _send_XYZ(double[] xyzpos)
        {
            for (int i = 0; i < 3; i++)
            {
                byte[] bytes = BitConverter.GetBytes((double)xyzpos[i]);
                Array.Reverse(bytes);
                _COM.Send(bytes, 8, SocketFlags.None);
            }
        }
        void _recv_XYZ(double[] xyzpos)
        {
            byte[] bytes = new byte[3 * 8];
            int nbytes = _COM.Receive(bytes, 3 * 8, SocketFlags.None);
            if (nbytes != 3 * 8)
            {
                throw new RdkException("Invalid pose sent"); //raise Exception('Problems running function');
            }
            for (int i = 0; i < 3; i++)
            {
                byte[] onedouble = new byte[8];
                Array.Copy(bytes, i * 8, onedouble, 0, 8);
                Array.Reverse(onedouble);
                xyzpos[i] = BitConverter.ToDouble(onedouble, 0);
            }
        }

        void _send_Int(Int32 number)
        {
            byte[] bytes = BitConverter.GetBytes(number);
            Array.Reverse(bytes); // convert from big endian to little endian
            _COM.Send(bytes);
        }

        Int32 _recv_Int()
        {
            byte[] bytes = new byte[4];
            int read = _COM.Receive(bytes, 4, SocketFlags.None);
            if (read < 4)
            {
                return 0;
            }
            Array.Reverse(bytes); // convert from little endian to big endian
            return BitConverter.ToInt32(bytes, 0);
        }

        // Sends an array of doubles
        void _send_Array(double[] values)
        {
            if (values == null)
            {
                _send_Int(0);
                return;
            }
            int nvalues = values.Length;
            _send_Int(nvalues);
            byte[] bytesarray = new byte[8 * nvalues];
            for (int i = 0; i < nvalues; i++)
            {
                byte[] onedouble = BitConverter.GetBytes(values[i]);
                Array.Reverse(onedouble);
                Array.Copy(onedouble, 0, bytesarray, i * 8, 8);
            }
            _COM.Send(bytesarray, 8 * nvalues, SocketFlags.None);
        }

        // Receives an array of doubles
        double[] _recv_Array()
        {
            int nvalues = _recv_Int();
            if (nvalues > 0)
            {
                double[] values = new double[nvalues];
                byte[] bytes = new byte[nvalues * 8];
                int read = _COM.Receive(bytes, nvalues * 8, SocketFlags.None);
                for (int i = 0; i < nvalues; i++)
                {
                    byte[] onedouble = new byte[8];
                    Array.Copy(bytes, i * 8, onedouble, 0, 8);
                    Array.Reverse(onedouble);
                    values[i] = BitConverter.ToDouble(onedouble, 0);
                }
                return values;
            }
            return null;
        }

        // sends a 2 dimensional matrix
        void _send_Matrix2D(Mat mat)
        {
            _send_Int(mat.Rows);
            _send_Int(mat.Cols);
            for (int j = 0; j < mat.Cols; j++)
            {
                for (int i = 0; i < mat.Rows; i++)
                {
                    byte[] bytes = BitConverter.GetBytes((double)mat[i, j]);
                    Array.Reverse(bytes);
                    _COM.Send(bytes, 8, SocketFlags.None);
                }
            }

        }

        // receives a 2 dimensional matrix (nxm)
        Mat _recv_Matrix2D()
        {
            int size1 = _recv_Int();
            int size2 = _recv_Int();
            int recvsize = size1 * size2 * 8;
            byte[] bytes = new byte[recvsize];
            Mat mat = new Mat(size1, size2);
            int BUFFER_SIZE = 256;
            int received = 0;
            if (recvsize > 0)
            {
                int to_receive = Math.Min(recvsize, BUFFER_SIZE);
                while (to_receive > 0)
                {
                    int nbytesok = _COM.Receive(bytes, received, to_receive, SocketFlags.None);
                    if (nbytesok <= 0)
                    {
                        throw new RdkException("Can't receive matrix properly"); //raise Exception('Problems running function');
                    }
                    received = received + nbytesok;
                    to_receive = Math.Min(recvsize - received, BUFFER_SIZE);
                }
            }
            int cnt = 0;
            for (int j = 0; j < mat.Cols; j++)
            {
                for (int i = 0; i < mat.Rows; i++)
                {
                    byte[] onedouble = new byte[8];
                    Array.Copy(bytes, cnt, onedouble, 0, 8);
                    Array.Reverse(onedouble);
                    mat[i, j] = BitConverter.ToDouble(onedouble, 0);
                    cnt = cnt + 8;
                }
            }
            return mat;
        }

        // private move type, to be used by public methods (MoveJ  and MoveL)
        void moveX(Item target, double[] joints, Mat mat_target, Item itemrobot, int movetype, bool blocking = true)
        {
            itemrobot.WaitMove();
            _send_Line("MoveX");
            _send_Int(movetype);
            if (target != null)
            {
                _send_Int(3);
                _send_Array(null);
                _send_Item(target);
            }
            else if (joints != null)
            {
                _send_Int(1);
                _send_Array(joints);
                _send_Item(null);
            }
            else if (mat_target != null && mat_target.IsHomogeneous())
            {
                _send_Int(2);
                _send_Array(mat_target.ToDoubles());
                _send_Item(null);
            }
            else
            {
                throw new RdkException("Invalid target type"); //raise Exception('Problems running function');
            }
            _send_Item(itemrobot);
            _check_status();
            if (blocking)
            {
                itemrobot.WaitMove();
            }
        }
        // private move type, to be used by public methods (MoveJ  and MoveL)
        void moveC_private(Item target1, double[] joints1, Mat mat_target1, Item target2, double[] joints2, Mat mat_target2, Item itemrobot, bool blocking = true)
        {
            itemrobot.WaitMove();
            _send_Line("MoveC");
            _send_Int(3);
            if (target1 != null)
            {
                _send_Int(3);
                _send_Array(null);
                _send_Item(target1);
            }
            else if (joints1 != null)
            {
                _send_Int(1);
                _send_Array(joints1);
                _send_Item(null);
            }
            else if (mat_target1 != null && mat_target1.IsHomogeneous())
            {
                _send_Int(2);
                _send_Array(mat_target1.ToDoubles());
                _send_Item(null);
            }
            else
            {
                throw new RdkException("Invalid type of target 1");
            }
            /////////////////////////////////////
            if (target2 != null)
            {
                _send_Int(3);
                _send_Array(null);
                _send_Item(target2);
            }
            else if (joints2 != null)
            {
                _send_Int(1);
                _send_Array(joints2);
                _send_Item(null);
            }
            else if (mat_target2 != null && mat_target2.IsHomogeneous())
            {
                _send_Int(2);
                _send_Array(mat_target2.ToDoubles());
                _send_Item(null);
            }
            else
            {
                throw new RdkException("Invalid type of target 2");
            }
            /////////////////////////////////////
            _send_Item(itemrobot);
            _check_status();
            if (blocking)
            {
                itemrobot.WaitMove();
            }
        }

        /// <summary>
        ///     Disconnect from the RoboDK API. This flushes any pending program generation.
        /// </summary>
        /// <returns></returns>
        internal void Finish()
        {
            Disconnect();
        }

        internal bool Set_connection_params(int safe_mode = 1, int auto_update = 0, int timeout = -1)
        {
            //Sets some behavior parameters: SAFE_MODE, AUTO_UPDATE and TIMEOUT.
            SAFE_MODE = safe_mode;
            AUTO_UPDATE = auto_update;
            if (timeout >= 0)
                TIMEOUT = timeout;
            _send_Line("CMD_START");
            _send_Line(Convert.ToString(SAFE_MODE) + " " + Convert.ToString(AUTO_UPDATE));
            var response = _recv_Line();
            if (response == "READY")
                return true;
            return false;
        }
    }
}