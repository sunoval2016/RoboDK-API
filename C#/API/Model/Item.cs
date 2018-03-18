// ----------------------------------------------------------------------------------------------------------
// Copyright 2018 - RoboDK Inc. - https://robodk.com/
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// http://www.apache.org/licenses/LICENSE-2.0
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// ----------------------------------------------------------------------------------------------------------

// ----------------------------------------------------------------------------------------------------------
// This file (RoboDK.cs) implements the RoboDK API for C#
// This file defines the following classes:
//     Mat: Matrix class, useful pose operations
//     RoboDK: API to interact with RoboDK
//     RoboDK.Item: Any item in the RoboDK station tree
//
// These classes are the objects used to interact with RoboDK and create macros.
// An item is an object in the RoboDK tree (it can be either a robot, an object, a tool, a frame, a program, ...).
// Items can be retrieved from the RoboDK station using the RoboDK() object (such as RoboDK.GetItem() method) 
//
// In this document: pose = transformation matrix = homogeneous matrix = 4x4 matrix
//
// More information about the RoboDK API for Python here:
//     https://robodk.com/doc/en/RoboDK-API.html
//     https://robodk.com/doc/en/PythonAPI/index.html
//
// More information about RoboDK post processors here:
//     https://robodk.com/help#PostProcessor
//
// Visit the Matrix and Quaternions FAQ for more information about pose/homogeneous transformations
//     http://www.j3d.org/matrix_faq/matrfaq_latest.html
//
// This library includes the mathematics to operate with homogeneous matrices for robotics.
// ----------------------------------------------------------------------------------------------------------

#region Namespaces

using System;
using System.Collections.Generic;
using System.Linq;
using RoboDK.API.Exceptions;

#endregion

namespace RoboDK.API.Model
{
    /// <summary>
    ///     The Item class represents an item in RoboDK station. An item can be a robot, a frame, a tool, an object, a target,
    ///     ... any item visible in the station tree.
    ///     An item can also be seen as a node where other items can be attached to (child items).
    ///     Every item has one parent item/node and can have one or more child items/nodes
    ///     RoboLinkItem is a "friend" class of RoboLink.
    /// </summary>
    public class Item
    {
        #region Fields

        private ulong _item;
        private readonly int _type;
        private string _name;
        //public RoboDK link;

        #endregion

        #region Constructors

        public Item(RoboDK connectionLink, ulong itemPtr = 0, int itemType = -1)
        {
            _item = itemPtr;
            link = connectionLink;
            _type = itemType;
        }

        #endregion

        #region Properties

        public RoboDK link { get; private set; }

        #endregion

        #region Public Methods

        public ulong get_item()
        {
            return _item;
        }

        public string ToString2()
        {
            if (Valid())
            {
                return $"RoboDK item {_item} of type {_type}";
            }
            return "RoboDK item (INVALID)";
        }

        /// <summary>
        /// Update item flags. 
        /// Item flags allow defining how much access the user has to item-specific features. 
        /// </summary>
        /// <param name="itemFlags">Item Flags to be set</param>
        public void SetItemFlags(ItemFlags itemFlags = ItemFlags.All)
        {
            int flags = (int)itemFlags;
            link._check_connection();
            string command = "S_Item_Rights";
            link.send_line(command);
            link.send_item(this);
            link.send_int(flags);
            link.check_status();
        }

        /// <summary>
        /// Retrieve current item flags. 
        /// Item flags allow defining how much access the user has to item-specific features. 
        /// </summary>
        /// <returns>Current Item Flags</returns>
        public ItemFlags GetItemFlags()
        {
            link._check_connection();
            string command = "S_Item_Rights";
            link.send_line(command);
            link.send_item(this);
            int flags = link.rec_int();
            ItemFlags itemFlags = (ItemFlags)flags;
            link.check_status();
            return itemFlags;
        }

        /// <summary>
        ///     Returns an integer that represents the type of the item (robot, object, tool, frame, ...)
        ///     Compare the returned value to ITEM_CASE_* variables
        /// </summary>
        /// <param name="item_other"></param>
        /// <returns></returns>
        public bool Equals(Item item_other)
        {
            return _item == item_other._item;
        }

        /// <summary>
        ///     Use RDK() instead. Returns the RoboDK link Robolink().
        /// </summary>
        /// <returns></returns>
        public RoboDK RL()
        {
            return link;
        }

        /// <summary>
        ///     Returns the RoboDK link Robolink().
        /// </summary>
        /// <returns></returns>
        public RoboDK RDK()
        {
            return link;
        }

        /// <summary>
        ///     Create a new communication link. Use this for robots if you use a multithread application running multiple robots
        ///     at the same time.
        /// </summary>
        public void NewLink()
        {
            link = new RoboDK();
        }

        //////// GENERIC ITEM CALLS

        //////// GENERIC ITEM CALLS
        /// <summary>
        /// Returns the type of an item (robot, object, target, reference frame, ...)
        /// </summary>
        /// <returns></returns>
        public int Type()
        {
            link._check_connection();
            link._send_Line("G_Item_Type");
            link._send_Item(this);
            int itemtype = link._recv_Int();
            link._check_status();
            return itemtype;
        }

        ////// add more methods

        /// <summary>
        /// Save a station or object to a file
        /// </summary>
        /// <param name="filename"></param>
        public void Save(string filename)
        {
            link.Save(filename, this);
        }

        /// <summary>
        /// Deletes an item and its childs from the station.
        /// </summary>
        public void Delete()
        {
            link._check_connection();
            link._send_Line("Remove");
            link._send_Item(this);
            link._check_status();
            _item = 0;
        }

        /// <summary>
        /// Checks if the item is valid. An invalid item will be returned by an unsuccessful function call.
        /// </summary>
        /// <returns>true if valid, false if invalid</returns>
        public bool Valid()
        {
            if (_item == 0)
            {
                return false;
            }
            return true;
        }

        ////// add more methods

        /// <summary>
        /// Return the parent item of this item (:class:`.Item`)
        /// </summary>
        /// <returns></returns>
        public Item Parent()
        {
            link._check_connection();
            link._send_Line("G_Parent");
            link._send_Item(this);
            Item parent = link._recv_Item();
            link._check_status();
            return parent;
        }


        /// <summary>
        /// Returns a list of the item childs that are attached to the provided item.
        /// </summary>
        /// <returns>item x n -> list of child items</returns>
        public Item[] Childs()
        {
            link._check_connection();
            link._send_Line("G_Childs");
            link._send_Item(this);
            int nitems = link._recv_Int();
            Item[] itemlist = new Item[nitems];
            for (int i = 0; i < nitems; i++)
            {
                itemlist[i] = link._recv_Item();
            }
            link._check_status();
            return itemlist;
        }

        /// <summary>
        /// Returns 1 if the item is visible, otherwise, returns 0.
        /// </summary>
        /// <returns>true if visible, false if not visible</returns>
        public bool Visible()
        {
            link._check_connection();
            link._send_Line("G_Visible");
            link._send_Item(this);
            int visible = link._recv_Int();
            link._check_status();
            return (visible != 0);
        }
        /// <summary>
        /// Sets the item visiblity status
        /// </summary>
        /// <param name="visible"></param>
        /// <param name="visible_frame">srt the visible reference frame (1) or not visible (0)</param>
        public void setVisible(bool visible, int visible_frame = -1)
        {
            if (visible_frame < 0)
            {
                visible_frame = visible ? 1 : 0;
            }
            link._check_connection();
            link._send_Line("S_Visible");
            link._send_Item(this);
            link._send_Int(visible ? 1 : 0);
            link._send_Int(visible_frame);
            link._check_status();
        }

        /// <summary>
        /// Returns the name of an item. The name of the item is always displayed in the RoboDK station tree
        /// </summary>
        /// <returns>name of the item</returns>
        public string Name()
        {
            link._check_connection();
            link._send_Line("G_Name");
            link._send_Item(this);
            string name = link._recv_Line();
            link._check_status();
            return name;
        }

        /// <summary>
        /// Set the name of a RoboDK item.
        /// </summary>
        /// <param name="name"></param>
        public void setName(string name)
        {
            link._check_connection();
            link._send_Line("S_Name");
            link._send_Item(this);
            link._send_Line(name);
            link._check_status();
        }

        // add more methods

        /// <summary>
        /// Sets the local position (pose) of an object, target or reference frame. For example, the position of an object/frame/target with respect to its parent.
        /// If a robot is provided, it will set the pose of the end efector.
        /// </summary>
        /// <param name="pose">4x4 homogeneous matrix</param>
        public void setPose(Mat pose)
        {
            link._check_connection();
            link._send_Line("S_Hlocal");
            link._send_Item(this);
            link._send_Pose(pose);
            link._check_status();
        }

        /// <summary>
        /// Returns the local position (pose) of an object, target or reference frame. For example, the position of an object/frame/target with respect to its parent.
        /// If a robot is provided, it will get the pose of the end efector
        /// </summary>
        /// <returns>4x4 homogeneous matrix (pose)</returns>
        public Mat Pose()
        {
            link._check_connection();
            link._send_Line("G_Hlocal");
            link._send_Item(this);
            Mat pose = link._recv_Pose();
            link._check_status();
            return pose;
        }

        /// <summary>
        /// Sets the position (pose) the object geometry with respect to its own reference frame. This procedure works for tools and objects.
        /// </summary>
        /// <param name="pose">4x4 homogeneous matrix</param>
        public void setGeometryPose(Mat pose)
        {
            link._check_connection();
            link._send_Line("S_Hgeom");
            link._send_Item(this);
            link._send_Pose(pose);
            link._check_status();
        }

        /// <summary>
        /// Returns the position (pose) the object geometry with respect to its own reference frame. This procedure works for tools and objects.
        /// </summary>
        /// <returns>4x4 homogeneous matrix (pose)</returns>
        public Mat GeometryPose()
        {
            link._check_connection();
            link._send_Line("G_Hgeom");
            link._send_Item(this);
            Mat pose = link._recv_Pose();
            link._check_status();
            return pose;
        }

        /// <summary>
        /// Obsolete: Use setPoseTool(pose) instead. Sets the tool pose of a tool item. If a robot is provided it will set the tool pose of the active tool held by the robot.
        /// </summary>
        /// <param name="pose">4x4 homogeneous matrix (pose)</param>
        public void setHtool(Mat pose)
        {
            link._check_connection();
            link._send_Line("S_Htool");
            link._send_Item(this);
            link._send_Pose(pose);
            link._check_status();
        }

        /// <summary>
        /// Obsolete: Use PoseTool() instead. 
        /// Returns the tool pose of an item. If a robot is provided it will get the tool pose of the active tool held by the robot.
        /// </summary>
        /// <returns>4x4 homogeneous matrix (pose)</returns>
        public Mat Htool()
        {
            link._check_connection();
            link._send_Line("G_Htool");
            link._send_Item(this);
            Mat pose = link._recv_Pose();
            link._check_status();
            return pose;
        }

        /// <summary>
        /// Returns the tool pose of an item. If a robot is provided it will get the tool pose of the active tool held by the robot.
        /// </summary>
        /// <returns>4x4 homogeneous matrix (pose)</returns>
        public Mat PoseTool()
        {
            link._check_connection();
            link._send_Line("G_Tool");
            link._send_Item(this);
            Mat pose = link._recv_Pose();
            link._check_status();
            return pose;
        }

        /// <summary>
        /// Returns the reference frame pose of an item. If a robot is provided it will get the tool pose of the active reference frame used by the robot.
        /// </summary>
        /// <returns>4x4 homogeneous matrix (pose)</returns>
        public Mat PoseFrame()
        {
            link._check_connection();
            link._send_Line("G_Frame");
            link._send_Item(this);
            Mat pose = link._recv_Pose();
            link._check_status();
            return pose;
        }

        /// <summary>
        /// Sets the reference frame of a robot(user frame). The frame can be either an item or a pose.
        /// If "frame" is an item, it links the robot to the frame item. If frame is a pose, it updates the linked pose of the robot frame (with respect to the robot reference frame).
        /// </summary>
        /// <param name="frame_pose">4x4 homogeneous matrix (pose)</param>
        public void setPoseFrame(Mat frame_pose)
        {
            link._check_connection();
            link._send_Line("S_Frame");
            link._send_Pose(frame_pose);
            link._send_Item(this);
            link._check_status();
        }

        /// <summary>
        /// Sets the tool of a robot or a tool object (Tool Center Point, or TCP). The tool pose can be either an item or a 4x4 Matrix.
        /// If the item is a tool, it links the robot to the tool item.If tool is a pose, it updates the current robot TCP.
        /// </summary>
        /// <param name="pose">4x4 homogeneous matrix (pose)</param>
        public void setPoseFrame(Item frame_item)
        {
            link._check_connection();
            link._send_Line("S_Frame_ptr");
            link._send_Item(frame_item);
            link._send_Item(this);
            link._check_status();
        }

        /// <summary>
        /// Sets the tool of a robot or a tool object (Tool Center Point, or TCP). The tool pose can be either an item or a 4x4 Matrix.
        /// If the item is a tool, it links the robot to the tool item.If tool is a pose, it updates the current robot TCP.
        /// </summary>
        /// <param name="tool_pose">4x4 homogeneous matrix (pose)</param>
        public void setPoseTool(Mat tool_pose)
        {
            link._check_connection();
            link._send_Line("S_Tool");
            link._send_Pose(tool_pose);
            link._send_Item(this);
            link._check_status();
        }

        /// <summary>
        /// Sets the tool of a robot or a tool object (Tool Center Point, or TCP). The tool pose can be either an item or a 4x4 Matrix.
        /// If the item is a tool, it links the robot to the tool item.If tool is a pose, it updates the current robot TCP.
        /// </summary>
        /// <param name="tool_item">Tool item</param>
        public void setPoseTool(Item tool_item)
        {
            link._check_connection();
            link._send_Line("S_Tool_ptr");
            link._send_Item(tool_item);
            link._send_Item(this);
            link._check_status();
        }

        /// <summary>
        /// Sets the global position (pose) of an item. For example, the position of an object/frame/target with respect to the station origin.
        /// </summary>
        /// <param name="pose">4x4 homogeneous matrix (pose)</param>
        public void setPoseAbs(Mat pose)
        {
            link._check_connection();
            link._send_Line("S_Hlocal_Abs");
            link._send_Item(this);
            link._send_Pose(pose);
            link._check_status();

        }

        /// <summary>
        /// Returns the global position (pose) of an item. For example, the position of an object/frame/target with respect to the station origin.
        /// </summary>
        /// <returns>4x4 homogeneous matrix (pose)</returns>
        public Mat PoseAbs()
        {
            link._check_connection();
            link._send_Line("G_Hlocal_Abs");
            link._send_Item(this);
            Mat pose = link._recv_Pose();
            link._check_status();
            return pose;
        }

        /// <summary>
        /// Changes the color of a robot/object/tool. A color must must in the format COLOR=[R,G,B,(A=1)] where all values range from 0 to 1.
        /// Alpha (A) defaults to 1 (100% opaque). Set A to 0 to make an object transparent.
        /// </summary>
        /// <param name="tocolor">color to change to</param>
        /// <param name="fromcolor">filter by this color</param>
        /// <param name="tolerance">optional tolerance to use if a color filter is used (defaults to 0.1)</param>
        public void Recolor(double[] tocolor, double[] fromcolor = null, double tolerance = 0.1)
        {
            link._check_connection();
            if (fromcolor == null)
            {
                fromcolor = new double[] { 0, 0, 0, 0 };
                tolerance = 2;
            }
            link.check_color(tocolor);
            link.check_color(fromcolor);
            link._send_Line("Recolor");
            link._send_Item(this);
            double[] combined = new double[9];
            combined[0] = tolerance;
            Array.Copy(fromcolor, 0, combined, 1, 4);
            Array.Copy(tocolor, 0, combined, 5, 4);
            link._send_Array(combined);
            link._check_status();
        }

        /// <summary>
        /// Apply a scale to an object to make it bigger or smaller.
        /// The scale can be uniform (if scale is a float value) or per axis (if scale is a vector).
        /// </summary>
        /// <param name="scale">scale to apply as [scale_x, scale_y, scale_z]</param>
        public void Scale(double[] scale)
        {
            link._check_connection();
            if (scale.Length != 3)
            {
                throw new RdkException("scale must be a single value or a 3-vector value");
            }
            link._send_Line("Scale");
            link._send_Item(this);
            link._send_Array(scale);
            link._check_status();
        }

        /// <summary>
        /// Adds a curve provided point coordinates. The provided points must be a list of vertices. A vertex normal can be provided optionally.
        /// </summary>
        /// <param name="curve_points">matrix 3xN or 6xN -> N must be multiple of 3</param>
        /// <param name="add_to_ref">add_to_ref -> If True, the curve will be added as part of the object in the RoboDK item tree</param>
        /// <param name="projection_type">Type of projection. For example: PROJECTION_ALONG_NORMAL_RECALC will project along the point normal and recalculate the normal vector on the surface projected.</param>
        /// <returns>returns the object where the curve was added or null if failed</returns>
        public Item AddCurve(Mat curve_points, bool add_to_ref = false, int projection_type = PROJECTION_ALONG_NORMAL_RECALC)
        {
            return link.AddCurve(curve_points, this, add_to_ref, projection_type);
        }

        /// <summary>
        /// Projects a point to the object given its coordinates. The provided points must be a list of [XYZ] coordinates. Optionally, a vertex normal can be provided [XYZijk].
        /// </summary>
        /// <param name="points">matrix 3xN or 6xN -> list of points to project</param>
        /// <param name="projection_type">projection_type -> Type of projection. For example: PROJECTION_ALONG_NORMAL_RECALC will project along the point normal and recalculate the normal vector on the surface projected.</param>
        /// <returns>projected points (empty matrix if failed)</returns>
        public Mat ProjectPoints(Mat points, int projection_type = PROJECTION_ALONG_NORMAL_RECALC)
        {
            return link.ProjectPoints(points, this, projection_type);
        }

        //"""Target item calls"""

        /// <summary>
        /// Sets a target as a cartesian target. A cartesian target moves to cartesian coordinates.
        /// </summary>
        public void setAsCartesianTarget()
        {
            link._check_connection();
            link._send_Line("S_Target_As_RT");
            link._send_Item(this);
            link._check_status();
        }

        /// <summary>
        /// Sets a target as a joint target. A joint target moves to a joints position without regarding the cartesian coordinates.
        /// </summary>
        public void setAsJointTarget()
        {
            link._check_connection();
            link._send_Line("S_Target_As_JT");
            link._send_Item(this);
            link._check_status();
        }

        /// <summary>
        /// Returns True if a target is a joint target (green icon). Otherwise, the target is a Cartesian target (red icon).
        /// </summary>
        public bool isJointTarget()
        {
            link._check_connection();
            link._send_Line("Target_Is_JT");
            link._send_Item(this);
            int is_jt = link._recv_Int();
            link._check_status();
            return is_jt > 0;
        }

        //#####Robot item calls####

        /// <summary>
        /// Returns the current joints of a robot or the joints of a target. If the item is a cartesian target, it returns the preferred joints (configuration) to go to that cartesian position.
        /// </summary>
        /// <returns>double x n -> joints matrix</returns>
        public double[] Joints()
        {
            link._check_connection();
            link._send_Line("G_Thetas");
            link._send_Item(this);
            double[] joints = link._recv_Array();
            link._check_status();
            return joints;
        }

        // add more methods

        /// <summary>
        /// Returns the home joints of a robot. These joints can be manually set in the robot "Parameters" menu, then select "Set home position"
        /// </summary>
        /// <returns>double x n -> joints array</returns>
        public double[] JointsHome()
        {
            link._check_connection();
            link._send_Line("G_Home");
            link._send_Item(this);
            double[] joints = link._recv_Array();
            link._check_status();
            return joints;
        }

        /// <summary>
        /// Sets the current joints of a robot or the joints of a target. It the item is a cartesian target, it returns the preferred joints (configuration) to go to that cartesian position.
        /// </summary>
        /// <param name="joints"></param>
        public void setJoints(double[] joints)
        {
            link._check_connection();
            link._send_Line("S_Thetas");
            link._send_Array(joints);
            link._send_Item(this);
            link._check_status();
        }

        /// <summary>
        /// Returns the joint limits of a robot
        /// </summary>
        /// <param name="lower_limits"></param>
        /// <param name="upper_limits"></param>
        public void JointLimits(double[] lower_limits, double[] upper_limits)
        {
            link._check_connection();
            link._send_Line("G_RobLimits");
            link._send_Item(this);
            lower_limits = link._recv_Array();
            upper_limits = link._recv_Array();
            double joints_type = link._recv_Int() / 1000.0;
            link._check_status();
        }

        /// <summary>
        /// Sets the robot of a program or a target. You must set the robot linked to a program or a target every time you copy paste these objects.
        /// If the robot is not provided, the first available robot will be chosen automatically.
        /// </summary>
        /// <param name="robot">Robot item</param>
        public void setRobot(Item robot = null)
        {
            link._check_connection();
            link._send_Line("S_Robot");
            link._send_Item(this);
            link._send_Item(robot);
            link._check_status();
        }

        /// <summary>
        /// Obsolete: Use setPoseFrame instead.
        /// Sets the frame of a robot (user frame). The frame can be either an item or a 4x4 Matrix.
        /// If "frame" is an item, it links the robot to the frame item. If frame is a 4x4 Matrix, it updates the linked pose of the robot frame.
        /// </summary>
        /// <param name="frame">item/pose -> frame item or 4x4 Matrix (pose of the reference frame)</param>
        public void setFrame(Item frame)
        {
            setPoseFrame(frame);
        }

        /// <summary>
        /// Obsolete: Use setPoseFrame instead.
        /// Sets the frame of a robot (user frame). The frame can be either an item or a 4x4 Matrix.
        /// If "frame" is an item, it links the robot to the frame item. If frame is a 4x4 Matrix, it updates the linked pose of the robot frame.
        /// </summary>
        /// <param name="frame">item/pose -> frame item or 4x4 Matrix (pose of the reference frame)</param>
        public void setFrame(Mat frame)
        {
            setPoseFrame(frame);
        }

        /// <summary>
        /// Obsolete: Use setPoseTool instead.
        /// Sets the tool pose of a robot. The tool pose can be either an item or a 4x4 Matrix.
        /// If "tool" is an item, it links the robot to the tool item. If tool is a 4x4 Matrix, it updates the linked pose of the robot tool.
        /// </summary>
        /// <param name="tool">item/pose -> tool item or 4x4 Matrix (pose of the tool frame)</param>
        public void setTool(Item tool)
        {
            setPoseTool(tool);
        }

        /// <summary>
        /// Obsolete: Use setPoseTool instead.
        /// Sets the tool pose of a robot. The tool pose can be either an item or a 4x4 Matrix.
        /// If "tool" is an item, it links the robot to the tool item. If tool is a 4x4 Matrix, it updates the linked pose of the robot tool.
        /// </summary>
        /// <param name="tool">item/pose -> tool item or 4x4 Matrix (pose of the tool frame)</param>
        public void setTool(Mat tool)
        {
            setPoseTool(tool);
        }

        /// <summary>
        /// Adds an empty tool to the robot provided the tool pose (4x4 Matrix) and the tool name.
        /// </summary>
        /// <param name="tool_pose">pose -> TCP as a 4x4 Matrix (pose of the tool frame)</param>
        /// <param name="tool_name">New tool name</param>
        /// <returns>new item created</returns>
        public Item AddTool(Mat tool_pose, string tool_name = "New TCP")
        {
            link._check_connection();
            link._send_Line("AddToolEmpty");
            link._send_Item(this);
            link._send_Pose(tool_pose);
            link._send_Line(tool_name);
            Item newtool = link._recv_Item();
            link._check_status();
            return newtool;
        }

        /// <summary>
        /// Computes the forward kinematics of the robot for the provided joints. The tool and the reference frame are not taken into account.
        /// </summary>
        /// <param name="joints"></param>
        /// <returns>4x4 homogeneous matrix: pose of the robot flange with respect to the robot base</returns>
        public Mat SolveFK(double[] joints)
        {
            link._check_connection();
            link._send_Line("G_FK");
            link._send_Array(joints);
            link._send_Item(this);
            Mat pose = link._recv_Pose();
            link._check_status();
            return pose;
        }

        /// <summary>
        /// Returns the robot configuration state for a set of robot joints.
        /// </summary>
        /// <param name="joints">array of joints</param>
        /// <returns>3-array -> configuration status as [REAR, LOWERARM, FLIP]</returns>
        public double[] JointsConfig(double[] joints)
        {
            link._check_connection();
            link._send_Line("G_Thetas_Config");
            link._send_Array(joints);
            link._send_Item(this);
            double[] config = link._recv_Array();
            link._check_status();
            return config;
        }

        /// <summary>
        /// Computes the inverse kinematics for the specified robot and pose. The joints returned are the closest to the current robot configuration (see SolveIK_All())
        /// </summary>
        /// <param name="pose">4x4 matrix -> pose of the robot flange with respect to the robot base frame</param>
        /// <returns>array of joints</returns>
        public double[] SolveIK(Mat pose)
        {
            link._check_connection();
            link._send_Line("G_IK");
            link._send_Pose(pose);
            link._send_Item(this);
            double[] joints = link._recv_Array();
            link._check_status();
            return joints;
        }

        /// <summary>
        /// Computes the inverse kinematics for the specified robot and pose. The function returns all available joint solutions as a 2D matrix.
        /// </summary>
        /// <param name="pose">4x4 matrix -> pose of the robot tool with respect to the robot frame</param>
        /// <returns>double x n x m -> joint list (2D matrix)</returns>
        public Mat SolveIK_All(Mat pose)
        {
            link._check_connection();
            link._send_Line("G_IK_cmpl");
            link._send_Pose(pose);
            link._send_Item(this);
            Mat joints_list = link._recv_Matrix2D();
            link._check_status();
            return joints_list;
        }

        /// <summary>
        /// Connect to a real robot using the robot driver.
        /// </summary>
        /// <param name="robot_ip">IP of the robot to connect. Leave empty to use the one defined in RoboDK</param>
        /// <returns>status -> true if connected successfully, false if connection failed</returns>
        public bool Connect(string robot_ip = "")
        {
            link._check_connection();
            link._send_Line("Connect");
            link._send_Item(this);
            link._send_Line(robot_ip);
            int status = link._recv_Int();
            link._check_status();
            return status != 0;
        }

        /// <summary>
        /// Disconnect from a real robot (when the robot driver is used)
        /// </summary>
        /// <returns>status -> true if disconnected successfully, false if it failed. It can fail if it was previously disconnected manually for example.</returns>
        public bool Disconnect()
        {
            link._check_connection();
            link._send_Line("Disconnect");
            link._send_Item(this);
            int status = link._recv_Int();
            link._check_status();
            return status != 0;
        }

        /// <summary>
        /// Moves a robot to a specific target ("Move Joint" mode). By default, this function blocks until the robot finishes its movements.
        /// </summary>
        /// <param name="target">target -> target to move to as a target item (RoboDK target item)</param>
        /// <param name="blocking">blocking -> True if we want the instruction to block until the robot finished the movement (default=true)</param>
        public void MoveJ(Item itemtarget, bool blocking = true)
        {
            link.moveX(itemtarget, null, null, this, 1, blocking);
        }

        /// <summary>
        /// Moves a robot to a specific target ("Move Joint" mode). By default, this function blocks until the robot finishes its movements.
        /// </summary>
        /// <param name="target">joints -> joint target to move to.</param>
        /// <param name="blocking">blocking -> True if we want the instruction to block until the robot finished the movement (default=true)</param>
        public void MoveJ(double[] joints, bool blocking = true)
        {
            link.moveX(null, joints, null, this, 1, blocking);
        }

        /// <summary>
        /// Moves a robot to a specific target ("Move Joint" mode). By default, this function blocks until the robot finishes its movements.
        /// </summary>
        /// <param name="target">pose -> pose target to move to. It must be a 4x4 Homogeneous matrix</param>
        /// <param name="blocking">blocking -> True if we want the instruction to block until the robot finished the movement (default=true)</param>
        public void MoveJ(Mat target, bool blocking = true)
        {
            link.moveX(null, null, target, this, 1, blocking);
        }

        /// <summary>
        /// Moves a robot to a specific target ("Move Linear" mode). By default, this function blocks until the robot finishes its movements.
        /// </summary>
        /// <param name="itemtarget">target -> target to move to as a target item (RoboDK target item)</param>
        /// <param name="blocking">blocking -> True if we want the instruction to block until the robot finished the movement (default=true)</param>
        public void MoveL(Item itemtarget, bool blocking = true)
        {
            link.moveX(itemtarget, null, null, this, 2, blocking);
        }

        /// <summary>
        /// Moves a robot to a specific target ("Move Linear" mode). By default, this function blocks until the robot finishes its movements.
        /// </summary>
        /// <param name="joints">joints -> joint target to move to.</param>
        /// <param name="blocking">blocking -> True if we want the instruction to block until the robot finished the movement (default=true)</param>
        public void MoveL(double[] joints, bool blocking = true)
        {
            link.moveX(null, joints, null, this, 2, blocking);
        }

        /// <summary>
        /// Moves a robot to a specific target ("Move Linear" mode). By default, this function blocks until the robot finishes its movements.
        /// </summary>
        /// <param name="target">pose -> pose target to move to. It must be a 4x4 Homogeneous matrix</param>
        /// <param name="blocking">blocking -> True if we want the instruction to block until the robot finished the movement (default=true)</param>
        public void MoveL(Mat target, bool blocking = true)
        {
            link.moveX(null, null, target, this, 2, blocking);
        }

        /// <summary>
        /// Moves a robot to a specific target ("Move Circular" mode). By default, this function blocks until the robot finishes its movements.
        /// </summary>
        /// <param name="itemtarget1">target -> intermediate target to move to as a target item (RoboDK target item)</param>
        /// <param name="itemtarget2">target -> final target to move to as a target item (RoboDK target item)</param>
        /// <param name="blocking">blocking -> True if we want the instruction to block until the robot finished the movement (default=true)</param>
        public void MoveC(Item itemtarget1, Item itemtarget2, bool blocking = true)
        {
            link.moveC_private(itemtarget1, null, null, itemtarget2, null, null, this, blocking);
        }

        /// <summary>
        /// Moves a robot to a specific target ("Move Circular" mode). By default, this function blocks until the robot finishes its movements.
        /// </summary>
        /// <param name="joints1">joints -> intermediate joint target to move to.</param>
        /// <param name="joints2">joints -> final joint target to move to.</param>
        /// <param name="blocking">blocking -> True if we want the instruction to block until the robot finished the movement (default=true)</param>
        public void MoveC(double[] joints1, double[] joints2, bool blocking = true)
        {
            link.moveC_private(null, joints1, null, null, joints2, null, this, blocking);
        }

        /// <summary>
        /// Moves a robot to a specific target ("Move Circular" mode). By default, this function blocks until the robot finishes its movements.
        /// </summary>
        /// <param name="target1">pose -> intermediate pose target to move to. It must be a 4x4 Homogeneous matrix</param>
        /// <param name="target2">pose -> final pose target to move to. It must be a 4x4 Homogeneous matrix</param>
        /// <param name="blocking">blocking -> True if we want the instruction to block until the robot finished the movement (default=true)</param>
        public void MoveC(Mat target1, Mat target2, bool blocking = true)
        {
            link.moveC_private(null, null, target1, null, null, target2, this, blocking);
        }

        /// <summary>
        /// Checks if a joint movement is free of collision.
        /// </summary>
        /// <param name="j1">joints -> start joints</param>
        /// <param name="j2">joints -> destination joints</param>
        /// <param name="minstep_deg">(optional): maximum joint step in degrees</param>
        /// <returns>collision : returns 0 if the movement is free of collision. Otherwise it returns the number of pairs of objects that collided if there was a collision.</returns>
        public int MoveJ_Test(double[] j1, double[] j2, double minstep_deg = -1)
        {
            link._check_connection();
            link._send_Line("CollisionMove");
            link._send_Item(this);
            link._send_Array(j1);
            link._send_Array(j2);
            link._send_Int((int)(minstep_deg * 1000.0));
            link._COM.ReceiveTimeout = 3600 * 1000;
            int collision = link._recv_Int();
            link._COM.ReceiveTimeout = link._TIMEOUT;
            link._check_status();
            return collision;
        }

        /// <summary>
        /// Checks if a linear movement is free of collision.
        /// </summary>
        /// <param name="j1">joints -> start joints</param>
        /// <param name="pose2">pose -> destination pose (active tool with respect to the active reference frame)</param>
        /// <param name="minstep_mm">(optional): maximum joint step in mm</param>
        /// <returns>collision : returns 0 if the movement is free of collision. Otherwise it returns the number of pairs of objects that collided if there was a collision.</returns>
        public int MoveL_Test(double[] j1, Mat pose2, double minstep_mm = -1)
        {
            link._check_connection();
            link._send_Line("CollisionMoveL");
            link._send_Item(this);
            link._send_Array(j1);
            link._send_Pose(pose2);
            link._send_Int((int)(minstep_mm * 1000.0));
            link._COM.ReceiveTimeout = 3600 * 1000;
            int collision = link._recv_Int();
            link._COM.ReceiveTimeout = link._TIMEOUT;
            link._check_status();
            return collision;
        }

        /// <summary>
        /// Sets the speed and/or the acceleration of a robot.
        /// </summary>
        /// <param name="speed">speed -> speed in mm/s (-1 = no change)</param>
        /// <param name="accel">acceleration (optional) -> acceleration in mm/s2 (-1 = no change)</param>
        /*
        public void setSpeed(double speed, double accel = -1)
        {
            link._check_connection();
            link._send_Line("S_Speed");
            link._send_Int((int)(speed * 1000.0));
            link._send_Int((int)(accel * 1000.0));
            link._send_Item(this);
            link._check_status();

        }*/

        /// <summary>
        /// Sets the speed and/or the acceleration of a robot.
        /// </summary>
        /// <param name="speed_linear">linear speed in mm/s (-1 = no change)</param>
        /// <param name="accel_linear">linear acceleration in mm/s2 (-1 = no change)</param>
        /// <param name="speed_joints">joint speed in deg/s (-1 = no change)</param>
        /// <param name="accel_joints">joint acceleration in deg/s2 (-1 = no change)</param>
        public void setSpeed(double speed_linear, double accel_linear = -1, double speed_joints = -1, double accel_joints = -1)
        {
            link._check_connection();
            link._send_Line("S_Speed4");
            link._send_Item(this);
            double[] speed_accel = new double[4];
            speed_accel[0] = speed_linear;
            speed_accel[1] = accel_linear;
            speed_accel[2] = speed_joints;
            speed_accel[3] = accel_joints;
            link._send_Array(speed_accel);
            link._check_status();

        }

        /// <summary>
        /// Sets the robot movement smoothing accuracy (also known as zone data value).
        /// </summary>
        /// <param name="rounding_mm">Rounding value (double) (robot dependent, set to -1 for accurate/fine movements)</param>
        public void setRounding(double rounding_mm)
        {
            link._check_connection();
            link._send_Line("S_ZoneData");
            link._send_Int((int)(rounding_mm * 1000.0));
            link._send_Item(this);
            link._check_status();
        }
        /// <summary>
        /// Obsolete, use setRounding instead
        /// </summary>
        public void setZoneData(double rounding_mm)
        {
            setRounding(rounding_mm);
        }

        /// <summary>
        /// Displays a sequence of joints
        /// </summary>
        /// <param name="sequence">joint sequence as a 6xN matrix or instruction sequence as a 7xN matrix</param>
        public void ShowSequence(Mat sequence)
        {
            link._check_connection();
            link._send_Line("Show_Seq");
            link._send_Matrix2D(sequence);
            link._send_Item(this);
            link._check_status();
        }


        /// <summary>
        /// Checks if a robot or program is currently running (busy or moving)
        /// </summary>
        /// <returns>busy status (true=moving, false=stopped)</returns>
        public bool Busy()
        {
            link._check_connection();
            link._send_Line("IsBusy");
            link._send_Item(this);
            int busy = link._recv_Int();
            link._check_status();
            return (busy > 0);
        }

        /// <summary>
        /// Stops a program or a robot
        /// </summary>
        /// <returns></returns>
        public void Stop()
        {
            link._check_connection();
            link._send_Line("Stop");
            link._send_Item(this);
            link._check_status();
        }

        /// <summary>
        /// Waits (blocks) until the robot finishes its movement.
        /// </summary>
        /// <param name="timeout_sec">timeout -> Max time to wait for robot to finish its movement (in seconds)</param>
        public void WaitMove(double timeout_sec = 300)
        {
            link._check_connection();
            link._send_Line("WaitMove");
            link._send_Item(this);
            link._check_status();
            link._COM.ReceiveTimeout = (int)(timeout_sec * 1000.0);
            link._check_status();//will wait here;
            link._COM.ReceiveTimeout = link._TIMEOUT;
            //int isbusy = link.Busy(this);
            //while (isbusy)
            //{
            //    busy = link.Busy(item);
            //}
        }

        ///////// ADD MORE METHODS


        // ---- Program item calls -----

        /// <summary>
        /// Sets the accuracy of the robot active or inactive. A robot must have been calibrated to properly use this option.
        /// </summary>
        /// <param name="accurate">set to 1 to use the accurate model or 0 to use the nominal model</param>
        public void setAccuracyActive(int accurate = 1)
        {
            link._check_connection();
            link._send_Line("S_AbsAccOn");
            link._send_Item(this);
            link._send_Int(accurate);
            link._check_status();
        }

        /// <summary>
        /// Saves a program to a file.
        /// </summary>
        /// <param name="filename">File path of the program</param>
        /// <returns>success</returns>
        public bool MakeProgram(string filename)
        {
            link._check_connection();
            link._send_Line("MakeProg");
            link._send_Item(this);
            link._send_Line(filename);
            int prog_status = link._recv_Int();
            string prog_log_str = link._recv_Line();
            link._check_status();
            bool success = false;
            if (prog_status > 1)
            {
                success = true;
            }
            return success; // prog_log_str
        }

        /// <summary>
        /// Sets if the program will be run in simulation mode or on the real robot.
        /// Use: "PROGRAM_RUN_ON_SIMULATOR" to set the program to run on the simulator only or "PROGRAM_RUN_ON_ROBOT" to force the program to run on the robot.
        /// </summary>
        /// <returns>number of instructions that can be executed</returns>
        public void setRunType(int program_run_type)
        {
            link._check_connection();
            link._send_Line("S_ProgRunType");
            link._send_Item(this);
            link._send_Int(program_run_type);
            link._check_status();
        }

        /// <summary>
        /// Runs a program. It returns the number of instructions that can be executed successfully (a quick program check is performed before the program starts)
        /// This is a non-blocking call. Use IsBusy() to check if the program execution finished.
        /// Notes:
        /// if setRunMode(RUNMODE_SIMULATE) is used  -> the program will be simulated (default run mode)
        /// if setRunMode(RUNMODE_RUN_ROBOT) is used -> the program will run on the robot (default when RUNMODE_RUN_ROBOT is used)
        /// if setRunMode(RUNMODE_RUN_ROBOT) is used together with program.setRunType(PROGRAM_RUN_ON_ROBOT) -> the program will run sequentially on the robot the same way as if we right clicked the program and selected "Run on robot" in the RoboDK GUI        
        /// </summary>
        /// <returns>number of instructions that can be executed</returns>
        public int RunProgram()
        {
            link._check_connection();
            link._send_Line("RunProg");
            link._send_Item(this);
            int prog_status = link._recv_Int();
            link._check_status();
            return prog_status;
        }


        /// <summary>
        /// Runs a program. It returns the number of instructions that can be executed successfully (a quick program check is performed before the program starts)
        /// Program parameters can be provided for Python calls.
        /// This is a non-blocking call.Use IsBusy() to check if the program execution finished.
        /// Notes: if setRunMode(RUNMODE_SIMULATE) is used  -> the program will be simulated (default run mode)
        /// if setRunMode(RUNMODE_RUN_ROBOT) is used ->the program will run on the robot(default when RUNMODE_RUN_ROBOT is used)
        /// if setRunMode(RUNMODE_RUN_ROBOT) is used together with program.setRunType(PROGRAM_RUN_ON_ROBOT) -> the program will run sequentially on the robot the same way as if we right clicked the program and selected "Run on robot" in the RoboDK GUI
        /// </summary>
        /// <param name="parameters">Number of instructions that can be executed</param>
        public int RunCode(string parameters = null)
        {
            link._check_connection();
            if (parameters == null)
            {
                link._send_Line("RunProg");
                link._send_Item(this);
            }
            else
            {
                link._send_Line("RunProgParam");
                link._send_Item(this);
                link._send_Line(parameters);
            }
            int progstatus = link._recv_Int();
            link._check_status();
            return progstatus;
        }

        /// <summary>
        /// Adds a program call, code, message or comment inside a program.
        /// </summary>
        /// <param name="code"><string of the code or program to run/param>
        /// <param name="run_type">INSTRUCTION_* variable to specify if the code is a progra</param>
        public int RunCodeCustom(string code, int run_type = INSTRUCTION_CALL_PROGRAM)
        {
            link._check_connection();
            link._send_Line("RunCode2");
            link._send_Item(this);
            link._send_Line(code.Replace("\n\n", "<br>").Replace("\n", "<br>"));
            link._send_Int(run_type);
            int progstatus = link._recv_Int();
            link._check_status();
            return progstatus;
        }

        /// <summary>
        /// Generates a pause instruction for a robot or a program when generating code. Set it to -1 (default) if you want the robot to stop and let the user resume the program anytime.
        /// </summary>
        /// <param name="time_ms">Time in milliseconds</param>
        public void Pause(double time_ms = -1)
        {
            link._check_connection();
            link._send_Line("RunPause");
            link._send_Item(this);
            link._send_Int((int)(time_ms * 1000.0));
            link._check_status();
        }


        /// <summary>
        /// Sets a variable (output) to a given value. This can also be used to set any variables to a desired value.
        /// </summary>
        /// <param name="io_var">io_var -> digital output (string or number)</param>
        /// <param name="io_value">io_value -> value (string or number)</param>
        public void setDO(string io_var, string io_value)
        {
            link._check_connection();
            link._send_Line("setDO");
            link._send_Item(this);
            link._send_Line(io_var);
            link._send_Line(io_value);
            link._check_status();
        }

        /// <summary>
        /// Waits for an input io_id to attain a given value io_value. Optionally, a timeout can be provided.
        /// </summary>
        /// <param name="io_var">io_var -> digital output (string or number)</param>
        /// <param name="io_value">io_value -> value (string or number)</param>
        /// <param name="timeout_ms">int (optional) -> timeout in miliseconds</param>
        public void waitDI(string io_var, string io_value, double timeout_ms = -1)
        {
            link._check_connection();
            link._send_Line("waitDI");
            link._send_Item(this);
            link._send_Line(io_var);
            link._send_Line(io_value);
            link._send_Int((int)(timeout_ms * 1000.0));
            link._check_status();
        }

        /// <summary>
        /// Add a custom instruction. This instruction will execute a Python file or an executable file.
        /// </summary>
        /// <param name="name"></param>
        /// <param name="path_run">path to run(relative to RoboDK/bin folder or absolute path)</param>
        /// <param name="path_icon">icon path(relative to RoboDK/bin folder or absolute path)</param>
        /// <param name="blocking">True if blocking, 0 if it is a non blocking executable trigger</param>
        /// <param name="cmd_run_on_robot">Command to run through the driver when connected to the robot</param>
        /// :param name: digital input (string or number)
        public void customInstruction(string name, string path_run, string path_icon = "", bool blocking = true, string cmd_run_on_robot = "")
        {
            link._check_connection();
            link._send_Line("InsCustom2");
            link._send_Item(this);
            link._send_Line(name);
            link._send_Line(path_run);
            link._send_Line(path_icon);
            link._send_Line(cmd_run_on_robot);
            link._send_Int(blocking ? 1 : 0);
            link._check_status();
        }

        /// <summary>
        /// Adds a new robot move joint instruction to a program.
        /// </summary>
        /// <param name="itemtarget">target to move to</param>
        public void addMoveJ(Item itemtarget)
        {
            link._check_connection();
            link._send_Line("Add_INSMOVE");
            link._send_Item(itemtarget);
            link._send_Item(this);
            link._send_Int(1);
            link._check_status();
        }

        /// <summary>
        /// Adds a new robot move linear instruction to a program.
        /// </summary>
        /// <param name="itemtarget">target to move to</param>
        public void addMoveL(Item itemtarget)
        {
            link._check_connection();
            link._send_Line("Add_INSMOVE");
            link._send_Item(itemtarget);
            link._send_Item(this);
            link._send_Int(2);
            link._check_status();
        }

        ////////// ADD MORE METHODS
        /// <summary>
        /// Returns the number of instructions of a program.
        /// </summary>
        /// <returns></returns>
        public int InstructionCount()
        {
            link._check_connection();
            link._send_Line("Prog_Nins");
            link._send_Item(this);
            int nins = link._recv_Int();
            link._check_status();
            return nins;
        }

        /// <summary>
        /// Returns the program instruction at position id
        /// </summary>
        /// <param name="ins_id"></param>
        /// <param name="name"></param>
        /// <param name="instype"></param>
        /// <param name="movetype"></param>
        /// <param name="isjointtarget"></param>
        /// <param name="target"></param>
        /// <param name="joints"></param>
        public void Instruction(int ins_id, out string name, out int instype, out int movetype, out bool isjointtarget, out Mat target, out double[] joints)
        {
            link._check_connection();
            link._send_Line("Prog_GIns");
            link._send_Item(this);
            link._send_Int(ins_id);
            name = link._recv_Line();
            instype = link._recv_Int();
            movetype = 0;
            isjointtarget = false;
            target = null;
            joints = null;
            if (instype == INS_TYPE_MOVE)
            {
                movetype = link._recv_Int();
                isjointtarget = link._recv_Int() > 0 ? true : false;
                target = link._recv_Pose();
                joints = link._recv_Array();
            }
            link._check_status();
        }

        /// <summary>
        /// Sets the program instruction at position id
        /// </summary>
        /// <param name="ins_id"></param>
        /// <param name="name"></param>
        /// <param name="instype"></param>
        /// <param name="movetype"></param>
        /// <param name="isjointtarget"></param>
        /// <param name="target"></param>
        /// <param name="joints"></param>
        public void setInstruction(int ins_id, string name, int instype, int movetype, bool isjointtarget, Mat target, double[] joints)
        {
            link._check_connection();
            link._send_Line("Prog_SIns");
            link._send_Item(this);
            link._send_Int(ins_id);
            link._send_Line(name);
            link._send_Int(instype);
            if (instype == INS_TYPE_MOVE)
            {
                link._send_Int(movetype);
                link._send_Int(isjointtarget ? 1 : 0);
                link._send_Pose(target);
                link._send_Array(joints);
            }
            link._check_status();
        }


        /// <summary>
        /// Returns the list of program instructions as an MxN matrix, where N is the number of instructions and M equals to 1 plus the number of robot axes.
        /// </summary>
        /// <param name="instructions">the matrix of instructions</param>
        /// <returns>Returns 0 if success</returns>
        public int InstructionList(out Mat instructions)
        {
            link._check_connection();
            link._send_Line("G_ProgInsList");
            link._send_Item(this);
            instructions = link._recv_Matrix2D();
            int errors = link._recv_Int();
            link._check_status();
            return errors;
        }

        /// <summary>
        /// Updates a program and returns the estimated time and the number of valid instructions.
        /// An update can also be applied to a robot machining project. The update is performed on the generated program.
        /// </summary>
        /// <param name="collision_check">check_collisions: Check collisions (COLLISION_ON -yes- or COLLISION_OFF -no-)</param>
        /// <param name="timeout_sec">Maximum time to wait for the update to complete (in seconds)</param>
        /// <param name="out_nins_time_dist">optional double array [3] = [valid_instructions, program_time, program_distance]</param>
        /// <param name="mm_step">Maximum step in millimeters for linear movements (millimeters). Set to -1 to use the default, as specified in Tools-Options-Motion.</param>
        /// <param name="deg_step">Maximum step for joint movements (degrees). Set to -1 to use the default, as specified in Tools-Options-Motion.</param>
        /// <returns>1.0 if there are no problems with the path or less than 1.0 if there is a problem in the path (ratio of problem)</returns>
        public double Update(int collision_check = COLLISION_OFF, int timeout_sec = 3600, double[] out_nins_time_dist = null, double mm_step = -1, double deg_step = -1)
        {
            link._check_connection();
            link._send_Line("Update2");
            link._send_Item(this);
            double[] values = { collision_check, mm_step, deg_step};
            link._send_Array(values);
            link._COM.ReceiveTimeout = timeout_sec * 1000;
            double[] return_values = link._recv_Array();
            link._COM.ReceiveTimeout = link._TIMEOUT;
            string readable_msg = link._recv_Line();
            link._check_status();
            double ratio_ok = return_values[3];
            if (out_nins_time_dist != null)
            {
                out_nins_time_dist[0] = return_values[0];
                out_nins_time_dist[1] = return_values[1];
                out_nins_time_dist[2] = return_values[2];
            }
            return ratio_ok;
        }



        /// <summary>
        /// Returns a list of joints an MxN matrix, where M is the number of robot axes plus 4 columns. Linear moves are rounded according to the smoothing parameter set inside the program.
        /// </summary>
        /// <param name="error_msg">Returns a human readable error message (if any)</param>
        /// <param name="joint_list">Returns the list of joints as [J1, J2, ..., Jn, ERROR, MM_STEP, DEG_STEP, MOVE_ID] if a file name is not specified</param>
        /// <param name="mm_step">Maximum step in millimeters for linear movements (millimeters)</param>
        /// <param name="deg_step">Maximum step for joint movements (degrees)</param>
        /// <param name="save_to_file">Provide a file name to directly save the output to a file. If the file name is not provided it will return the matrix. If step values are very small, the returned matrix can be very large.</param>
        /// <param name="collision_check">Check for collisions: will set to 1 or 0</param>
        /// <param name="flags">Reserved for future compatibility</param>
        /// <returns>Returns 0 if success, otherwise, it will return negative values</returns>
        public int InstructionListJoints(out string error_msg, out Mat joint_list, double mm_step = 10.0, double deg_step = 5.0, string save_to_file = "", int collision_check = COLLISION_OFF, int flags = 0, int timeout_sec=3600)
        {
            link._check_connection();
            link._send_Line("G_ProgJointList");
            link._send_Item(this);
            double[] ste_mm_deg = { mm_step, deg_step, collision_check, flags };
            link._send_Array(ste_mm_deg);
            //joint_list = save_to_file;
            link._COM.ReceiveTimeout = 3600 * 1000;
            if (save_to_file.Length <= 0)
            {
                link._send_Line("");
                joint_list = link._recv_Matrix2D();
            }
            else
            {
                link._send_Line(save_to_file);
                joint_list = null;
            }
            
            int error_code = link._recv_Int();
            link._COM.ReceiveTimeout = link._TIMEOUT;
            error_msg = link._recv_Line();
            link._check_status();
            return error_code;
        }

        /// <summary>
        /// Disconnect from the RoboDK API. This flushes any pending program generation.
        /// </summary>
        /// <returns></returns>
        public bool Finish()
        {
            return link.Finish();
        }

        #endregion
    }
}