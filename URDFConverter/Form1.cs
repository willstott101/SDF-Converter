using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Runtime.InteropServices;
using System.Windows.Forms;

using URDF;
using Inventor;

namespace URDFConverter
{
    public partial class Form1 : Form
    {
        Inventor.Application _invApp;
        Robot robo;
        bool _started = false;

        public Form1()
        {
            InitializeComponent();

            #region Get Inventor session
            try
            {
                _invApp = (Inventor.Application)Marshal.GetActiveObject("Inventor.Application");
            }
            catch (Exception ex)
            {
                try
                {
                    Type invAppType = Type.GetTypeFromProgID("Inventor.Application");

                    _invApp = (Inventor.Application)System.Activator.CreateInstance(invAppType);
                    _invApp.Visible = true;

                    /* Note: if the Inventor session is left running after this
                     * form is closed, there will still an be and Inventor.exe 
                     * running. We will use this Boolean to test in Form1.Designer.cs 
                     * in the dispose method whether or not the Inventor App should
                     * be shut down when the form is closed.
                     */
                    _started = true;

                }
                catch (Exception ex2)
                {
                    MessageBox.Show(ex2.ToString());
                    MessageBox.Show("Unable to get or start Inventor");
                }
            }

            #endregion

            #region Test code
            /*
            // Define a new Robot, robot, with the name "HuboPlus"
            Robot robot = new Robot("HuboPlus");
            
            // Define a new Link, link1, with the name "link1".
            Link link1 = new Link("link1");
            
            // Set the Visual attributes, geometric and material, of the link.
            link1.Visual = new Visual(new Mesh("package://link1.stl"), 
                new URDF.Material("Red", new double[] { 255, 0, 0, 1.0 }));

            // Set the Collision attributes of the link.
            link1.Collision = new Collision(new URDF.Cylinder(1, 2));

            // Set the Inertial attributes of the link.
            link1.Inertial = new Inertial(5, new double[] { 1, 0, 0, 1, 0, 1 });

            // Add the link to the list of links within the robot.
            robot.Links.Add(link1);

            // Make a clone of link1 and add it to the robot model.
            robot.Links.Add((Link)link1.Clone());


            // Define a new Joint, joint1, with the name "joint1".
            Joint joint1 = new Joint("joint1", JointType.Prismatic, link1, link1);

            robot.Joints.Add(joint1);

            robot.Joints.Add((Joint)joint1.Clone());

            robot.WriteURDFToFile("C:\\Users\\W.Stott\\Documents\\URDF\\robo.xml");
            */

            #endregion

            //Test for inventor document, set some UI stuff.
            Reload();
        }

        public void Reload()
        {
            //Start robot
            robo = new Robot(_invApp.ActiveDocument.DisplayName);

            //Did we recieve an assembly document?
            if (_invApp.ActiveDocumentType == DocumentTypeEnum.kAssemblyDocumentObject) {
                this.label1.Text = "Found inventor assembly: " + _invApp.ActiveDocument.DisplayName;
                this.buttonGen.Show();
            }
            else
            {
                this.label1.Text = "Found inventor part, please open an assembly document.";
            }
        }

        public void RefreshView()
        {
            
        }

        public void WriteURDF()
        {
            UnitsOfMeasure oUOM = _invApp.ActiveDocument.UnitsOfMeasure;
            AssemblyDocument oAsmDoc = (AssemblyDocument)_invApp.ActiveDocument;
            AssemblyComponentDefinition oAsmCompDef = oAsmDoc.ComponentDefinition;
            ComponentOccurrence Parent;
            string ParentName, AbsolutePosition, name, mirname, mirParentName;
            double[] ParentCOM, Offset;

            foreach (ComponentOccurrence oCompOccur in oAsmCompDef.Occurrences)
            {
                // Generate links from available subassemblies in main assembly.

                //New Link
                robo.Links.Add(new Link(oCompOccur.Name));
                int c = robo.Links.Count - 1;

                //Find and set parent link
                for (int i = 0; i < robo.Links.Count; i++)
                {
                    if (String.Equals(robo.Links[i].Name, ReturnParentName(oCompOccur)))
                        robo.Links[c].Parent = robo.Links[i];
                }

                //If link has a parent
                if (robo.Links[c].Parent != null)
                {
                    //Define a joint
                    robo.Joints.Add(new Joint(FormatJointName(robo.Links[c].Name), JointType.Revolute, robo.Links[c].Parent, robo.Links[c]));
                    int j = robo.Joints.Count - 1;

                    //Parse joint axis
                    switch (robo.Joints[j].Name[robo.Joints[j].Name.Length - 1])
                    {
                        case 'R':
                            robo.Joints[j].Axis = new double[] { 1, 0, 0 };
                            break;
                        case 'P':
                            robo.Joints[j].Axis = new double[] { 0, 1, 0 };
                            break;
                        case 'Y':
                            robo.Joints[j].Axis = new double[] { 0, 0, 1 };
                            break;
                        default:
                            break;
                    }
                }

                // Get mass properties for each link.
                double[] iXYZ = new double[6];
                oCompOccur.MassProperties.XYZMomentsOfInertia(out iXYZ[0], out iXYZ[3], out iXYZ[5], out iXYZ[1], out iXYZ[4], out iXYZ[2]); // Ixx, Iyy, Izz, Ixy, Iyz, Ixz -> Ixx, Ixy, Ixz, Iyy, Iyz, Izz
                robo.Links[c].Inertial = new Inertial(oCompOccur.MassProperties.Mass, iXYZ);
                robo.Links[c].Inertial.XYZ = FindCenterOfMassOffset(oCompOccur);

                // Set shape properties for each link.
                robo.Links[c].Visual = new Visual(new Mesh("package://" + robo.Name + "/" + robo.Links[c].Name + ".stl"));
            }

        }

        public double[] ComputeRelativeOffset(ComponentOccurrence Child, ComponentOccurrence Parent)
        {
            double[] c1 = FindOrigin(Parent);
            double[] c2 = FindOrigin(Child);
            double[] c3 = new double[3];

            for (int k = 0; k < 3; k++)
            {
                c3[k] = c2[k] - c1[k];
            }

            return c3;
        }

        public double[] FindOrigin(ComponentOccurrence oCompOccur)
        {
            UnitsOfMeasure oUOM = _invApp.ActiveDocument.UnitsOfMeasure;
            AssemblyComponentDefinition oCompDef = (AssemblyComponentDefinition)oCompOccur.Definition;
            object oWorkPointProxy;
            double[] c = new double[3];
            WorkPoint oWP = oCompDef.WorkPoints[1];
            oCompOccur.CreateGeometryProxy(oWP, out oWorkPointProxy);

            c[0] = ((WorkPointProxy)oWorkPointProxy).Point.X;
            c[1] = ((WorkPointProxy)oWorkPointProxy).Point.Y;
            c[2] = ((WorkPointProxy)oWorkPointProxy).Point.Z;

            for (int k = 0; k < 3; k++)
            {
                c[k] = oUOM.ConvertUnits(c[k], "cm", "m");
            }

            string AbsolutePosition, name;
            name = FormatName(oCompOccur.Name);

            return c;
        }

        public int CheckBody(string strData)
        {
            // Match Bodies to actually export based on naming convention
            MatchCollection REMatches = Regex.Matches(strData, "^Body_", RegexOptions.IgnoreCase);

            return REMatches.Count;
        }

        public double[] FindCenterOfMassOffset(ComponentOccurrence oDoc)
        {
            // Store temporary variables and names
            MassProperties oMassProps = oDoc.MassProperties;
            double[] c = new double[3];

            c[0] = oMassProps.CenterOfMass.X;
            c[1] = oMassProps.CenterOfMass.Y;
            c[2] = oMassProps.CenterOfMass.Z;

            UnitsOfMeasure oUOM = _invApp.ActiveDocument.UnitsOfMeasure;

            for (int k = 0; k < 3; k++)
            {
                c[k] = oUOM.ConvertUnits(c[k], "cm", "m");
            }

            return c;
        }

        public string ReturnParentName(ComponentOccurrence occur)
        {
            try
            {
                return occur.Definition.Document.PropertySets.Item("Inventor User Defined Properties").Item("Parent").Value;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
                return null;
            }
        }

        public string FormatName(string strData)
        {
            // Match Bodies to actually export based on naming convention
            string res = strData;

            try
            {
                res = res.Split(':')[0];
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }

            return res;
        }

        public string FormatJointName(string strData)
        {
            // Match Bodies to actually export based on naming convention
            int Count;

            Match REMatches = Regex.Match(strData, "[RPY]:[0-9]", RegexOptions.IgnoreCase);

            Count = REMatches.Length;

            return REMatches.Value;
        }

        public ComponentOccurrence FindComponentOccurrence(ComponentOccurrences Comp, string name)
        {
            foreach (ComponentOccurrence occur in Comp)
            {
                if (occur.Name.IndexOf(name) >= 0)
                {
                    return occur;
                }
            }
            return null;
        }

        private void buttonGen_Click(object sender, EventArgs e)
        {
            //generate a new URDF
            WriteURDF();
        }

        private void buttonSave_Click(object sender, EventArgs e)
        {
            // Save the URDF
            SaveFileDialog saveFileDialog1 = new SaveFileDialog();

            saveFileDialog1.Filter = "URDF File (ROS) (*.xml)|*.xml";
            saveFileDialog1.RestoreDirectory = true;
            if (saveFileDialog1.ShowDialog() == DialogResult.OK)
            {
                robo.WriteURDFToFile(saveFileDialog1.FileName);
            }
        }

        private void buttonReload_Click(object sender, EventArgs e)
        {
            // Reload info, and reset URDF
            Reload();
        }

    }
}
