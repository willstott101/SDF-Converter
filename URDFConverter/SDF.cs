﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Runtime.Serialization.Formatters.Binary;
using System.Xml;
using System.Xml.Schema;
using System.Windows.Media.Media3D;
using System.Windows.Forms;


using MathNet.Numerics;
using System.Text.RegularExpressions;

namespace SDF
{
    #region Robot
    /// <summary>
    /// Defines the SDF Robot model.
    /// </summary>
    [Serializable]
    public class Robot : ICloneable
    {
        public string Name { get; set; }
        public Pose Pose = new Pose(0,0,0, 1);
        public List<Link> Links = new List<Link>();
        public List<Joint> Joints = new List<Joint>();

        public Robot(string name)
        {
            this.Name = name;
        }

        /// <summary>
        /// Clones the Robot object into a new object.
        /// </summary>
        /// <returns>Cloned Robot object.</returns>
        public object Clone()
        {
            MemoryStream ms = new MemoryStream();
            BinaryFormatter bf = new BinaryFormatter();
            bf.Serialize(ms, this);
            ms.Position = 0;
            object obj = bf.Deserialize(ms);
            ms.Close();
            return obj;
        }

        public void WriteSDFToWriter(XmlWriter SDFWriter, string name, int precision)
        {
            SDFWriter.WriteStartDocument(false);
            SDFWriter.WriteComment(" Exported at " + DateTime.Now.ToString() + " ");
            SDFWriter.WriteStartElement("sdf");
            SDFWriter.WriteAttributeString("version", "1.5");
            SDFWriter.WriteStartElement("model");
            SDFWriter.WriteAttributeString("name", name);
            Pose.PrintPoseTag((XmlTextWriter)SDFWriter, precision);

            foreach (Link link in Links)
            {
                link.PrintLinkTag((XmlTextWriter)SDFWriter, precision, name);
            }

            foreach (Joint joint in Joints)
            {
                joint.PrintJointTag((XmlTextWriter)SDFWriter, precision);
            }

            SDFWriter.WriteEndElement();//</model>
            SDFWriter.WriteEndElement();//</sdf>

            //Write the XML to file and close the writer
            SDFWriter.Flush();
        }

        public String WriteSDFToString(int precision)
        {
            using (var sw = new StringWriter())
            {
                using (var SDFWriter = XmlWriter.Create(sw))
                {
                    WriteSDFToWriter(SDFWriter, "test_string", precision);
                  
                }
                return sw.ToString();
            }
        }

        public void WriteSDFToFile(string filepath, int precision)
        {
            XmlTextWriter SDFWriter = new XmlTextWriter(filepath + "\\model.sdf", null);

            String modelname = filepath.Split(new Char[] { '\\' })[filepath.Split(new Char[] { '\\' }).Count() - 1];

            WriteSDFToWriter((XmlWriter)SDFWriter, modelname, precision);

            SDFWriter.Formatting = Formatting.Indented;
            SDFWriter.Indentation = 4;

            //Write the XML to file and close the writer
            //SDFWriter.Flush();
            SDFWriter.Close();
            if (SDFWriter != null)
                SDFWriter.Close();

            //Write model.config
            SDFWriter = new XmlTextWriter(filepath + "\\model.config", null);
            SDFWriter.WriteStartDocument(false);

            SDFWriter.WriteStartElement("model");
            SDFWriter.WriteElementString("name", modelname);
            SDFWriter.WriteElementString("version", "1.0");
            SDFWriter.WriteStartElement("sdf");
            SDFWriter.WriteAttributeString("version", "1.5");
            SDFWriter.WriteRaw("model.sdf");
            SDFWriter.WriteEndElement();
            SDFWriter.WriteEndElement();

            SDFWriter.Flush();
            SDFWriter.Close();
            if (SDFWriter != null)
                SDFWriter.Close();
        }
    }
    #endregion

    #region Link
    /// <summary>
    /// Defines the SDF Link model.
    /// </summary>
    [Serializable]
    public class Link : ICloneable
    {
        public string Name { get; set; }
        public Link Parent { get; set; }
        public Pose Pose { get; set; }
        public Inertial Inertial { get; set; }
        public Visual Visual { get; set; }
        public Collision Collision { get; set; }
        public List<Collision> CollisionGroup { get; set; }

        public Link(string name)
        {
            this.Name = name;
        }

        /// <summary>
        /// Clones the Link object into a new object.
        /// </summary>
        /// <returns>Cloned Link object.</returns>
        public object Clone()
        {
            MemoryStream ms = new MemoryStream();
            BinaryFormatter bf = new BinaryFormatter();
            bf.Serialize(ms, this);
            ms.Position = 0;
            object obj = bf.Deserialize(ms);
            ms.Close();
            return obj;
        }

        public void PrintLinkTag(XmlTextWriter SDFWriter, int precision, String foldername)
        {
            /* <link name="...">
             *     <inertial>
             *         ...
             *     </inertial>
             *     <visual>
             *         ...
             *     </visual>
             *     <collision>
             *         ...
             *     </collision>
             * </link>
             */
            SDFWriter.WriteStartElement("link");
            SDFWriter.WriteAttributeString("name", this.Name);
            if (this.Pose != null)
            {
                this.Pose.PrintPoseTag(SDFWriter, precision);
            }
            if (this.Inertial != null)
            {
                this.Inertial.PrintInertialTag(SDFWriter, precision);
            }
            if (this.Visual != null)
            {
                this.Visual.PrintVisualTag(SDFWriter, this.Name, foldername);
            }
            if (this.Collision != null)
            {
                this.Collision.PrintCollisionTag(SDFWriter, this.Name, foldername);
            }
            SDFWriter.WriteEndElement();
        }
    }

    /// <summary>
    /// Link position and rotation.
    /// </summary>
    [Serializable]
    public class Pose
    {
        public double[] Position { get; set; }
        public double[] Rotation { get; set; }

        public double[] axisAngle { get; private set; }
        public Inventor.Matrix matrix {get; private set;}

        private int internalprecision = 8;

        //If rotation given, we need more info
        public Pose(Inventor.Matrix R1, int precision, double positionScale = 1)
        {
            //Get global position and rotation
            Inventor.Vector pos = R1.Translation;
            internalprecision = precision;
            matrix = R1;

            //Calculate rotation.
            double[] Er = new double[3] { 0, 0, 0 };
            double cy_thresh = 0.00000004;
            double cy = Math.Sqrt(Math.Pow(R1.get_Cell(3, 3), 2) + Math.Pow(R1.get_Cell(3, 2), 2));
            if (cy > cy_thresh)
            {
                Er[2] = Math.Atan2(R1.get_Cell(2, 1), R1.get_Cell(1, 1));//Z
                Er[1] = Math.Atan2(-R1.get_Cell(3, 1), cy);//Y
                Er[0] = Math.Atan2(R1.get_Cell(3, 2), R1.get_Cell(3, 3));//X
            }
            else
            {
                Er[2] = Math.Atan2(R1.get_Cell(1, 3), R1.get_Cell(2, 2));//Z
                Er[1] = Math.Atan2(-R1.get_Cell(3, 1), cy);//Y
                Er[0] = 0;//X
            }

            Position = new double[3] {pos.X, pos.Y, pos.Z};
            Rotation = Er;

            Scale(positionScale);

            SetAxisAngle(R1);
        }

        public Pose(double x, double y, double z, int precision)
        {
            Position = new double[3] { x, y, z };
            Rotation = new double[3] { 0, 0, 0 };
        }

        public void Round(int precision)
        {
            //Set rounded values.
            int i = 0;
            for (i = 0; i < 3; i++)
            {
                Rotation[i] = Math.Round(Rotation[i], precision);
                Position[i] = Math.Round(Position[i], precision);
            }
        }

        public void SetRelative(Pose pose)
        {
            int i;

            for (i = 0; i < 3; i++)
            {
                Position[i] -= pose.Position[i];
            }

            if (pose.matrix != null)
            {
                //pose.matrix*Position
                Inventor.Matrix M = pose.matrix;

                int x;
                int y;
                string str = "Pose Relativematrix: " + Environment.NewLine;
                double[] dub = new double[1000];
                for (x = 1; x < 4; x++)
                {
                    for (y = 1; y < 4; y++)
                    {
                        str += M.get_Cell(x, y) + " ";
                    }
                    str += Environment.NewLine;
                }
                MessageBox.Show(str);

                Vector3D[] vec = new Vector3D[3];
                for (i = 0; i < 3; i++)
                {
                    vec[i].X = M.get_Cell(1, i + 1);
                    vec[i].Y = M.get_Cell(2, i + 1);
                    vec[i].Z = M.get_Cell(3, i + 1);
                }
                Vector3D output = Position[0] * vec[0] + Position[1] * vec[1] + Position[2] * vec[2];

                Position = new double[] { output.X, output.Y, output.Z };
            }
            
        }

        public void Scale (double scale)
        {
            int i;
            for (i=0; i < 3; i++) {
                Position[i] *= scale;
            }
        }

        public string ToString()
        {
            Round(internalprecision);
            return Position[0] + " " + Position[1] + " " + Position[2] + " " + Rotation[0] + " " + Rotation[1] + " " + Rotation[2];
        }

        public void PrintPoseTag(XmlTextWriter SDFWriter, int precision)
        {
            Round(precision);
            SDFWriter.WriteStartElement("pose");
            string pos = Position[0].ToString() + " " + Position[1].ToString() + " " + Position[2].ToString();
            string rot = Rotation[0].ToString() + " " + Rotation[1].ToString() + " " + Rotation[2].ToString();
            SDFWriter.WriteRaw(pos + " " + rot);
            SDFWriter.WriteEndElement();
        }

        public void SetAxisAngle(Inventor.Matrix R1) {
            
            /*R1.SetToIdentity();
            R1.set_Cell(1, 1, Math.Cos(0.78539816339));
            R1.set_Cell(1, 2, -Math.Sin(0.78539816339));
            R1.set_Cell(2, 1, Math.Sin(0.78539816339));
            R1.set_Cell(2, 2, Math.Cos(0.78539816339));*/
            int x;
            int y;
            string str = "Pose matrix: " + Environment.NewLine;
            double[] dub = new double[1000];
            for (x = 1; x < 4; x++ )
            {
                for (y = 1; y < 4; y++)
                {
                    str += R1.get_Cell(x, y) + " ";
                }
                str += Environment.NewLine;
            }
            MessageBox.Show(str);

            //Axis
            double[] axis = new double[3] { 0, 0, 0};
            axis[0] = R1.get_Cell(3, 2) - R1.get_Cell(2, 3);
            axis[1] = R1.get_Cell(1, 3) - R1.get_Cell(3, 1);
            axis[2] = R1.get_Cell(2, 1) - R1.get_Cell(1, 2);
            //Angle
            double r = Math.Sqrt(Math.Pow(axis[1], 2) + Math.Pow(axis[2], 2));
            r = Math.Sqrt(Math.Pow(axis[0], 2) + Math.Pow(r, 2));
            double t = R1.get_Cell(1, 1) + R1.get_Cell(2, 2) + R1.get_Cell(3, 3);
            double theta = Math.Atan2(r, t - 1);
            //Normalise
            int i;
            for (i = 0; i < 3; i++) {
                axis[i] = axis[i] / r;
            }

            //Return
            axisAngle = new double[] { theta, axis[0], axis[1], axis[2]};


            //MessageBox.Show("Pose Axis-Angle: " + Environment.NewLine + theta+ " " + axis[0]+ " " + axis[1]+ " " + axis[2]);

            /* Convert the rotation matrix into the axis-angle notation.

                Conversion equations
                ====================

                From Wikipedia (http://en.wikipedia.org/wiki/Rotation_matrix), the conversion is given by::

                    x = Qzy-Qyz
                    y = Qxz-Qzx
                    z = Qyx-Qxy
                    r = hypot(x,hypot(y,z))
                    t = Qxx+Qyy+Qzz
                    theta = atan2(r,t-1)
            */
        }
    }

    /// <summary>
    /// Link inertial properties.
    /// </summary>
    [Serializable]
    public class Inertial
    {
        public double Mass { get; set; }
        public double[,] InertiaMatrix { get; private set; }
        public double[] InertiaVector { get; private set; }

        /// <summary>
        /// Set link's mass and moment of inertia.
        /// </summary>
        /// <param name="mass">Link mass (Kg).</param>
        /// <param name="inertiaMatrix">3x3 element moment of inertia matrix (Kg*m^2) [Ixx Ixy Ixz; Ixy Iyy Iyz; Ixz Iyz Izz]</param>
        public Inertial(double mass, double[,] inertiaMatrix)
        {
            this.Mass = mass;
            this.InertiaMatrix = inertiaMatrix;
            this.ProcessMatrix();
        }

        /// <summary>
        /// Set link's mass and moment of inertia.
        /// </summary>
        /// <param name="mass">Link mass (Kg).</param>
        /// <param name="inertiaVector">1x6 vector of principal moments and products of inertia (Kg*m^2) [Ixx Ixy Ixz Iyy Iyz Izz]</param>
        public Inertial(double mass, double[] inertiaVector)
        {
            this.Mass = mass;
            this.InertiaVector = inertiaVector;
            this.ProcessVector();
        }

        public void ProcessVector()
        {
            this.InertiaMatrix = new double[,] { 
                { this.InertiaVector[0], this.InertiaVector[1], this.InertiaVector[2] },
                { this.InertiaVector[1], this.InertiaVector[3], this.InertiaVector[4] },
                { this.InertiaVector[2], this.InertiaVector[4], this.InertiaVector[5] } };
        }

        public void ProcessMatrix()
        {
            this.InertiaVector = new double[] { this.InertiaMatrix[0, 0], 
                this.InertiaMatrix[0, 1], 
                this.InertiaMatrix[0, 2], 
                this.InertiaMatrix[1, 1], 
                this.InertiaMatrix[1, 2], 
                this.InertiaMatrix[2, 2] };
        }

        public void Round(int precision)
        {
            int i;
            for (i = 0; i < 6;i++ )
            {
                InertiaVector[i] = Math.Round(InertiaVector[i], precision);
            }
            this.ProcessMatrix();
        }

        public void PrintInertialTag(XmlTextWriter SDFWriter, int precision)
        {
            /* <inertial>
             *     <origin xyz="# # #" rpy="# # #"/>
             *     <mass value="#"/>
             *     <inertia ixx="#"  ixy="#"  ixz="#" iyy="#" iyz="#" izz="#" />
             * </inertial>
             */
            this.Round(precision);
            SDFWriter.WriteStartElement("inertial");
            SDFWriter.WriteElementString("mass", this.Mass.ToString());
            SDFWriter.WriteStartElement("inertia");
            SDFWriter.WriteElementString("ixx", this.InertiaVector[0].ToString());
            SDFWriter.WriteElementString("ixy", this.InertiaVector[1].ToString());
            SDFWriter.WriteElementString("ixz", this.InertiaVector[2].ToString());
            SDFWriter.WriteElementString("iyy", this.InertiaVector[3].ToString());
            SDFWriter.WriteElementString("iyz", this.InertiaVector[4].ToString());
            SDFWriter.WriteElementString("izz", this.InertiaVector[5].ToString());
            SDFWriter.WriteEndElement();
            SDFWriter.WriteEndElement();
        }
    }

    /// <summary>
    /// Link visual properties.
    /// </summary>
    [Serializable]
    public class Visual
    {
        public Shape Shape { get; private set; }
        public Material Material { get; private set; }

        public Visual(Shape shape)
        {
            this.Shape = shape;
        }

        public Visual(Shape shape, Material material)
        {
            this.Shape = shape;
            this.Material = material;
        }

        public void PrintVisualTag(XmlTextWriter SDFWriter, String linkName, String foldername)
        {
            /* <visual>
             *     <pose># # # # # #</pose>
             *     <geometry>
             *         ...
             *     </geometry>
             *     <material>
             *         ...
             *     </material>
             * </visual>
             */
            SDFWriter.WriteStartElement("visual");
            SDFWriter.WriteAttributeString("name", linkName + "_vis");
            this.Shape.PrintGeometryTag(SDFWriter, foldername);
            if (Material != null)
            {
                this.Material.PrintMaterialTag(SDFWriter);
            }
            SDFWriter.WriteEndElement();
        }
    }

    /// <summary>
    /// Link material properties.
    /// </summary>
    [Serializable]
    public class Material
    {
        public string Name { get; set; }
        public double[] ColorRGBA { get; set; }

        public Material(string name, double[] colorRGBA)
        {
            this.Name = name;
            this.ColorRGBA = colorRGBA;
        }

        public void PrintMaterialTag(XmlTextWriter SDFWriter)
        {
            /* <material name="...">
             *     <color rgba="# # # #"/>
             * </material>
             */
            SDFWriter.WriteStartElement("material");
            SDFWriter.WriteAttributeString("name", this.Name);
            SDFWriter.WriteStartElement("color");
            SDFWriter.WriteAttributeString("rgba", this.ColorRGBA[0].ToString() + " "
                + this.ColorRGBA[1].ToString() + " "
                + this.ColorRGBA[2].ToString() + " "
                + this.ColorRGBA[3].ToString() + " ");
            SDFWriter.WriteEndElement();
            SDFWriter.WriteEndElement();
        }

    }

    /// <summary>
    /// Link collision properties.
    /// </summary>
    [Serializable]
    public class Collision
    {
        public Shape Shape { get; set; }

        public Collision(Shape shape)
        {
            this.Shape = shape;
        }

        public void PrintCollisionTag(XmlTextWriter SDFWriter, String linkName, String foldername)
        {
            /* <collision>
             *     <pose># # # # # #</pose>
             *     <geometry>
             *         ...
             *     </geometry>
             * </collision>
             */
            SDFWriter.WriteStartElement("collision");
            SDFWriter.WriteAttributeString("name", linkName + "_col");
            this.Shape.PrintGeometryTag(SDFWriter, foldername);
            SDFWriter.WriteEndElement();
        }
    }

    /// <summary>
    /// 
    /// </summary>
    [Serializable]
    public class Shape
    {
        protected double[] Size = new double[3];
        protected double Radius, Length, Scale;
        protected string Filename;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="size"></param>
        /// <param name="radius"></param>
        /// <param name="length"></param>
        /// <param name="filename"></param>
        /// <param name="scale"></param>
        public Shape(double[] size, double radius, double length, string filename, double scale)
        {
            this.Size = size;
            this.Radius = radius;
            this.Length = length;
            this.Filename = filename;
            this.Scale = scale;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="SDFWriter"></param>
        public virtual void PrintGeometryTag(XmlTextWriter SDFWriter, String foldername)
        {
            // Insert code into inherited classes.
        }
    }

    /// <summary>
    /// 
    /// </summary>
    [Serializable]
    public class Box : Shape
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="size">Extents of the box.</param>
        public Box(double[] size)
            : base(size, 0, 0, null, 0)
        {
        }

        public override void PrintGeometryTag(XmlTextWriter SDFWriter, String foldername)
        {
            /* <geometry>
             *     <box size="# # #"/>
             * </geometry>
             */
            SDFWriter.WriteStartElement("geometry");
            SDFWriter.WriteStartElement("box");
            SDFWriter.WriteAttributeString("size", Size[0].ToString() + " " + Size[1].ToString() + " " + Size[2].ToString());
            SDFWriter.WriteEndElement();
            SDFWriter.WriteEndElement();
        }
    }

    /// <summary>
    /// 
    /// </summary>
    [Serializable]
    public class Cylinder : Shape
    {
        public Cylinder(double radius, double length)
            : base(null, radius, length, null, 0)
        {
        }

        public override void PrintGeometryTag(XmlTextWriter SDFWriter, String foldername)
        {
            /* <geometry>
             *     <cylinder radius="#" length="#"/>
             * </geometry>
             */
            SDFWriter.WriteStartElement("geometry");
            SDFWriter.WriteStartElement("cylinder");
            SDFWriter.WriteAttributeString("radius", Radius.ToString());
            SDFWriter.WriteAttributeString("length", Length.ToString());
            SDFWriter.WriteEndElement();
            SDFWriter.WriteEndElement();
        }
    }

    /// <summary>
    /// 
    /// </summary>
    [Serializable]
    public class Sphere : Shape
    {
        public Sphere(double radius)
            : base(null, radius, 0, null, 0)
        {
        }

        public override void PrintGeometryTag(XmlTextWriter SDFWriter, String foldername)
        {
            /* <geometry>
             *     <sphere radius="#"/>
             * </geometry>
             */
            SDFWriter.WriteStartElement("geometry");
            SDFWriter.WriteStartElement("cylinder");
            SDFWriter.WriteAttributeString("radius", Radius.ToString());
            SDFWriter.WriteEndElement();
            SDFWriter.WriteEndElement();
        }

    }

    /// <summary>
    /// 
    /// </summary>
    [Serializable]
    public class Mesh : Shape
    {

        public Mesh(string filename)
            : base(null, 0, 0, filename, 1)
        {
        }

        public Mesh(string filename, double scale)
            : base(null, 0, 0, filename, scale)
        {
        }

        public override void PrintGeometryTag(XmlTextWriter SDFWriter, String foldername)
        {
            /* <geometry>
             *     <sphere filename="package://..." scale="#"/>
             * </geometry>
             */
            SDFWriter.WriteStartElement("geometry");
            SDFWriter.WriteStartElement("mesh");
            //TODO: Neaten up passing down values
            SDFWriter.WriteElementString("uri", Filename.Replace("<MODELNAME>", foldername));
            if (this.Scale != null && this.Scale != 1)
            {
                SDFWriter.WriteElementString("scale", this.Scale.ToString() + " " + this.Scale.ToString() + " " + this.Scale.ToString());
            }
            SDFWriter.WriteEndElement();
            SDFWriter.WriteEndElement();
        }
    }

    #endregion

    #region Joint
    /// <summary>
    /// Defines the SDF Joint model.
    /// </summary>
    [Serializable]
    public class Joint
    {
        public string Name { get; set; }
        public JointType JointType { get; set; }
        public Link Parent { get; set; }
        public Link Child { get; set; }
        public Limit Limit { get; set; }
        public Pose Pose { get; set; }

        public Axis Axis { get; set; }
        public Calibration Calibration { get; set; }
        public Dynamics Dynamics { get; set; }
        public SafetyController SafetyController { get; set; }

        public Joint(string name, JointType jointType)
        {
            this.Name = name;
            this.JointType = jointType;
            if (this.JointType == JointType.Revolute || this.JointType == JointType.Prismatic)
            {
                // Default values for limit that can be modified later.
                //this.Limit = new Limit(1.0, 30.0, 0.0, 180.0);
            }
        }

        public Joint(string name, JointType jointType, Link parent, Link child)
        {
            this.Name = name;
            this.JointType = jointType;
            if (this.JointType == JointType.Revolute || this.JointType == JointType.Prismatic)
            {
                // Default values for limit that can be modified later.
                //this.Limit = new Limit(1.0, 30.0, 0.0, 180.0);
            }
            this.Parent = parent;
            this.Child = child;
        }

        /// <summary>
        /// Clones the Joint object into a new object.
        /// </summary>
        /// <returns>Cloned Joint object.</returns>
        public object Clone()
        {
            MemoryStream ms = new MemoryStream();
            BinaryFormatter bf = new BinaryFormatter();
            bf.Serialize(ms, this);
            ms.Position = 0;
            object obj = bf.Deserialize(ms);
            ms.Close();
            return obj;
        }

        public void PrintJointTag(XmlTextWriter SDFWriter, int precision)
        {
            /* <joint name="..." type="...">
             *     <pose># # # # # #<pose/>
             *     <parent></parent>
             *     <child></child>
             *     
             *     <axis xyz="# # #"/>
             *     //<calibration 'type'(rising/falling)="#"/>
             *     //<dynamics damping="#" friction="#"/>
             *     //<limit ... />
             *     //<safety_controller
             * </joint>
             */
            SDFWriter.WriteStartElement("joint");
            SDFWriter.WriteAttributeString("name", this.Name);
            SDFWriter.WriteAttributeString("type", this.JointType.Type);
            if (this.Parent != null)
            {
                SDFWriter.WriteStartElement("parent");
                SDFWriter.WriteRaw(this.Parent.Name);
                SDFWriter.WriteEndElement();
            }
            if (this.Child != null)
            {
                SDFWriter.WriteStartElement("child");
                SDFWriter.WriteRaw(this.Child.Name);
                SDFWriter.WriteEndElement();
            }

            if (Pose != null) {
                Pose.PrintPoseTag(SDFWriter, precision);
            }

            if (this.Axis != null)
            {
                this.Axis.PrintAxisTag(SDFWriter);
            }

            if (this.Calibration != null)
            {
                SDFWriter.WriteStartElement("calibration");
                SDFWriter.WriteAttributeString(this.Calibration.Type, this.Calibration.Value.ToString());
                SDFWriter.WriteEndElement();
            }

            if (this.Dynamics != null)
            {
                this.Dynamics.PrintDynamicsTag(SDFWriter);
            }

            if (this.Limit != null)
            {
                this.Limit.PrintLimitTag(SDFWriter);
            }

            if (this.SafetyController != null)
            {
                this.SafetyController.PrintSafetyTag(SDFWriter);
            }

            SDFWriter.WriteEndElement();
        }
    }

    /// <summary>
    /// AXIS
    /// </summary>
    [Serializable]
    public class Axis
    {
        public double[] values { get; private set; }
        private int internalprecision = 8;

        public Axis(double x, double y, double z)
        {
            if (x < 0) { x *= -1; }
            if (y < 0) { y *= -1; }
            if (z < 0) { z *= -1; }

            values = new double[3] { x, y, z };
        }

        public Axis(double x, double y, double z, Pose child)
        {
            values = new double[3] { x, y, z };

            //Skip if the parent Pose doesn't have enough rotation information.
            if (child.axisAngle == null)
            {
                if (x < 0) { x *= -1; }
                if (y < 0) { y *= -1; }
                if (z < 0) { z *= -1; }

                values = new double[3] { x, y, z };

                return;
            }

            Quaternion Wg = new Quaternion(x, y, z, 0);
            //MessageBox.Show(x+" "+ y+" "+ z+" "+ 0);

            //Quaternion Wg = new Quaternion(0, 0, 1, 0);

            double[] Rc = child.axisAngle;
            Quaternion qC = new Quaternion(new Vector3D(Rc[1], Rc[2], Rc[3]), Rc[0]*180/Math.PI);

            Quaternion qC1 = qC;
            qC1.Invert();

            Quaternion Wc = qC * (Wg * qC1);

            Quaternion Qt = new Quaternion(0, 0.707120, 0, 0.70712);
            Quaternion Wt = new Quaternion(1, 0, 0, 0);
            Quaternion Qt1 = Qt;
            Qt1.Invert();
            Quaternion Wt_ = Qt * Wt * Qt1;

            values = new double[3] { Wc.X, Wc.Y, Wc.Z };

            if (values[0] < 0) { values[0] *= -1; }
            if (values[1] < 0) { values[1] *= -1; }
            if (values[2] < 0) { values[2] *= -1; }
        }

        public void Round(int precision)
        {
            int i;
            for (i = 0; i < 3; i++)
            {
                values[i] = Math.Round(values[i], precision);
            }
        }

        public string ToString()
        {
            if (values != null)
            {
                Round(internalprecision);
                return values[0].ToString() + " " + values[1].ToString() + " " + values[2].ToString();
            }
            else
            {
                return "null";
            }
        }

        public void PrintAxisTag(XmlTextWriter SDFWriter)
        {
            SDFWriter.WriteStartElement("axis");
            SDFWriter.WriteElementString("xyz", this.values[0] + " " + this.values[1] + " " + this.values[2]);
            SDFWriter.WriteElementString("use_parent_model_frame", "true");
            SDFWriter.WriteEndElement();
        }
    }

    /// <summary>
    /// 
    /// </summary>
    [Serializable]
    public class Limit
    {
        public double Effort { get; set; }
        public double Velocity { get; set; }
        public double Lower { get; set; }
        public double Upper { get; set; }

        public Limit(double effort, double velocity, double lower, double upper)
        {
            this.Effort = effort;
            this.Velocity = velocity;
            this.Lower = lower;
            this.Upper = upper;
        }

        public void PrintLimitTag(XmlTextWriter SDFWriter)
        {
            // <limit effort="#" velocity="#" lower="#" upper="#"/>
            SDFWriter.WriteStartElement("limit");
            SDFWriter.WriteAttributeString("effort", this.Effort.ToString());
            SDFWriter.WriteAttributeString("velocity", this.Velocity.ToString());
            SDFWriter.WriteAttributeString("lower", this.Lower.ToString());
            SDFWriter.WriteAttributeString("upper", this.Upper.ToString());
            SDFWriter.WriteEndElement();
        }
    }

    /// <summary>
    /// 
    /// </summary>
    [Serializable]
    public class Dynamics
    {
        public double Damping { get; set; }
        public double Friction { get; set; }

        public Dynamics(double damping, double friction)
        {
            this.Damping = damping;
            this.Friction = friction;
        }

        public void PrintDynamicsTag(XmlTextWriter SDFWriter)
        {
            SDFWriter.WriteStartElement("dynamics");
            SDFWriter.WriteAttributeString("damping", this.Damping.ToString());
            SDFWriter.WriteAttributeString("friction", this.Friction.ToString());
            SDFWriter.WriteEndElement();
        }
    }

    /// <summary>
    /// 
    /// </summary>
    [Serializable]
    public class SafetyController
    {
        public double SoftLowerLimit { get; set; }
        public double SoftUpperLimit { get; set; }
        public double KPosition { get; set; }
        public double KVelocity { get; set; }

        public SafetyController(double softLowerLimit, double softUpperLimit, double kPosition, double kVelocity)
        {
            this.SoftLowerLimit = softLowerLimit;
            this.SoftUpperLimit = softUpperLimit;
            this.KPosition = kPosition;
            this.KVelocity = kVelocity;
        }

        public void PrintSafetyTag(XmlTextWriter SDFWriter)
        {
            SDFWriter.WriteStartElement("safety_controller");
            SDFWriter.WriteAttributeString("soft_lower_limit", this.SoftLowerLimit.ToString());
            SDFWriter.WriteAttributeString("soft_upper_limit", this.SoftUpperLimit.ToString());
            SDFWriter.WriteAttributeString("k_position", this.KPosition.ToString());
            SDFWriter.WriteAttributeString("k_velocity", this.KVelocity.ToString());
            SDFWriter.WriteEndElement();
        }
    }

    /// <summary>
    /// 
    /// </summary>
    [Serializable]
    public sealed class JointType
    {
        public static readonly JointType Revolute = new JointType("revolute");
        public static readonly JointType Continuous = new JointType("continuous");
        public static readonly JointType Prismatic = new JointType("prismatic");
        public static readonly JointType Fixed = new JointType("fixed");
        public static readonly JointType Floating = new JointType("floating");
        public static readonly JointType Planar = new JointType("planar");

        private JointType(string type)
        {
            Type = type;
        }

        public string Type { get; private set; }
    }

    /// <summary>
    /// 
    /// </summary>
    [Serializable]
    public sealed class Calibration
    {
        public static readonly Calibration Rising = new Calibration("rising", 0.0);
        public static readonly Calibration Falling = new Calibration("falling", 0.0);

        private Calibration(string type, double value)
        {
            Type = type;
            Value = value;
        }

        public string Type { get; private set; }
        public double Value { get; set; }
    }

    #endregion
}
