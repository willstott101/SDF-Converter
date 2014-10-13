using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Runtime.Serialization.Formatters.Binary;
using System.Xml;
using System.Xml.Schema;

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
        public Pose Pose = new Pose(0,0,0);
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

        public void WriteSDFToWriter(XmlWriter SDFWriter)
        {
            SDFWriter.WriteStartDocument(false);
            SDFWriter.WriteComment(" Exported at " + DateTime.Now.ToString() + " ");
            SDFWriter.WriteStartElement("sdf");
            SDFWriter.WriteAttributeString("version", "1.5");
            SDFWriter.WriteStartElement("model");
            SDFWriter.WriteAttributeString("name", this.Name);
            Pose.PrintPoseTag((XmlTextWriter)SDFWriter);

            foreach (Link link in Links)
            {
                link.PrintLinkTag((XmlTextWriter)SDFWriter);
            }

            foreach (Joint joint in Joints)
            {
                joint.PrintJointTag((XmlTextWriter)SDFWriter);
            }

            SDFWriter.WriteEndElement();//</model>
            SDFWriter.WriteEndElement();//</sdf>

            //Write the XML to file and close the writer
            SDFWriter.Flush();
        }

        public String WriteSDFToString()
        {
            using (var sw = new StringWriter())
            {
                using (var SDFWriter = XmlWriter.Create(sw))
                {
                    WriteSDFToWriter(SDFWriter);
                  
                }
                return sw.ToString();
            }
        }

        public void WriteSDFToFile(string filename)
        {
            XmlTextWriter SDFWriter = new XmlTextWriter(filename, null);

            WriteSDFToWriter((XmlWriter)SDFWriter);

            SDFWriter.Formatting = Formatting.Indented;

            //Write the XML to file and close the writer
            //SDFWriter.Flush();
            SDFWriter.Close();
            if (SDFWriter != null)
                SDFWriter.Close();
        }

        public void WriteSDFToFileOld(string filename)
        {
            XmlTextWriter SDFWriter = new XmlTextWriter(filename, null);
            SDFWriter.Formatting = Formatting.Indented;
            SDFWriter.WriteStartDocument(false);
            SDFWriter.WriteComment(" Exported at " + DateTime.Now.ToString() + " ");
            SDFWriter.WriteStartElement("robot");
            SDFWriter.WriteAttributeString("name", this.Name);

            foreach (Link link in Links)
            {
                link.PrintLinkTag(SDFWriter);
            }

            foreach (Joint joint in Joints)
            {
                joint.PrintJointTag(SDFWriter);
            }

            SDFWriter.WriteEndElement();

            //Write the XML to file and close the writer
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

        public void PrintLinkTag(XmlTextWriter SDFWriter)
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
                this.Pose.PrintPoseTag(SDFWriter);
            }
            if (this.Inertial != null)
            {
                this.Inertial.PrintInertialTag(SDFWriter);
            }
            if (this.Visual != null)
            {
                this.Visual.PrintVisualTag(SDFWriter, this.Name);
            }
            if (this.Collision != null)
            {
                this.Collision.PrintCollisionTag(SDFWriter, this.Name);
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

        public Pose(double x, double y, double z, double Ex, double Ey, double Ez)
        {
            Position = new double[3] {x,y,z};
            Rotation = new double[3] {Ex,Ey,Ez};
        }

        public Pose(double x, double y, double z)
        {
            Position = new double[3] { x, y, z };
            Rotation = new double[3] { 0, 0, 0 };
        }

        public void PrintPoseTag(XmlTextWriter SDFWriter)
        {
            SDFWriter.WriteStartElement("pose");
            string pos = Position[0].ToString() + " " + Position[1].ToString() + " " + Position[2].ToString();
            string rot = Rotation[0].ToString() + " " + Rotation[1].ToString() + " " + Rotation[2].ToString();
            SDFWriter.WriteRaw(pos + " " + rot);
            SDFWriter.WriteEndElement();
        }
    }

    /// <summary>
    /// Link inertial properties.
    /// </summary>
    [Serializable]
    public class Inertial : Origin
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
            this.InertiaVector = new double[] { inertiaMatrix[0, 0], 
                inertiaMatrix[0, 1], 
                inertiaMatrix[0, 2], 
                inertiaMatrix[1, 1], 
                inertiaMatrix[1, 2], 
                inertiaMatrix[2, 2] };
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
            this.InertiaMatrix = new double[,] { 
                { inertiaVector[0], inertiaVector[1], inertiaVector[2] },
                { inertiaVector[1], inertiaVector[3], inertiaVector[4] },
                { inertiaVector[2], inertiaVector[4], inertiaVector[5] } };
        }

        public void PrintInertialTag(XmlTextWriter SDFWriter)
        {
            /* <inertial>
             *     <origin xyz="# # #" rpy="# # #"/>
             *     <mass value="#"/>
             *     <inertia ixx="#"  ixy="#"  ixz="#" iyy="#" iyz="#" izz="#" />
             * </inertial>
             */
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
    public class Visual : Origin
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

        public void PrintVisualTag(XmlTextWriter SDFWriter, String linkName)
        {
            /* <visual>
             *     <origin ... />
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
            this.Shape.PrintGeometryTag(SDFWriter);
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
    public class Collision : Origin
    {
        public Shape Shape { get; set; }

        public Collision(Shape shape)
        {
            this.Shape = shape;
        }

        public void PrintCollisionTag(XmlTextWriter SDFWriter, String linkName)
        {
            /* <collision>
             *     <origin ... />
             *     <geometry>
             *         ...
             *     </geometry>
             * </collision>
             */
            SDFWriter.WriteStartElement("collision");
            SDFWriter.WriteAttributeString("name", linkName + "_col");
            this.Shape.PrintGeometryTag(SDFWriter);
            SDFWriter.WriteEndElement();
        }
    }

    /// <summary>
    /// Link and Joint origin properties.
    /// </summary>
    [Serializable]
    public class Origin
    {
        public double[] XYZ { get; set; }
        public double[] RPY { get; set; }

        public void PrintOriginTag(XmlTextWriter SDFWriter)
        {
            // <origin xyz="# # #" rpy="# # #"/>
            if (XYZ != null && RPY != null)
            {
                SDFWriter.WriteStartElement("origin");
                if (XYZ != null)
                {
                    SDFWriter.WriteAttributeString("xyz", XYZ[0].ToString() + " " + XYZ[1].ToString() + " " + XYZ[2].ToString());
                }
                if (RPY != null)
                {
                    SDFWriter.WriteAttributeString("rpy", RPY[0].ToString() + " " + RPY[1].ToString() + " " + RPY[2].ToString());
                }
                SDFWriter.WriteEndElement();
            }
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
        public virtual void PrintGeometryTag(XmlTextWriter SDFWriter)
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

        public override void PrintGeometryTag(XmlTextWriter SDFWriter)
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

        public override void PrintGeometryTag(XmlTextWriter SDFWriter)
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

        public override void PrintGeometryTag(XmlTextWriter SDFWriter)
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

        public override void PrintGeometryTag(XmlTextWriter SDFWriter)
        {
            /* <geometry>
             *     <sphere filename="package://..." scale="#"/>
             * </geometry>
             */
            SDFWriter.WriteStartElement("geometry");
            SDFWriter.WriteStartElement("mesh");
            SDFWriter.WriteElementString("uri", Filename);
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
    public class Joint : Origin
    {
        public string Name { get; set; }
        public JointType JointType { get; set; }
        public Link Parent { get; set; }
        public Link Child { get; set; }
        public Limit Limit { get; set; }

        public double[] Axis { get; set; }
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

        public void PrintJointTag(XmlTextWriter SDFWriter)
        {
            /* <joint name="..." type="...">
             *     <origin ... />
             *     <parent link="..."/>
             *     <child link="..."/>
             *     
             *     <axis xyz="# # #"/>
             *     <calibration 'type'(rising/falling)="#"/>
             *     <dynamics damping="#" friction="#"/>
             *     <limit ... />
             *     <safety_controller
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

            if (this.Axis != null)
            {
                SDFWriter.WriteStartElement("axis");
                SDFWriter.WriteStartElement("xyz");
                SDFWriter.WriteRaw(this.Axis[0] + " " + this.Axis[1] + " " + this.Axis[2]);
                SDFWriter.WriteEndElement();
                SDFWriter.WriteEndElement();
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
