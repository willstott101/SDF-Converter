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

using SDF;
using Inventor;
using System.Diagnostics;
using MathNet.Numerics.LinearAlgebra;
using MathNet.Numerics.LinearAlgebra.Double;

namespace SDFConverter
{
    public partial class Form1 : Form
    {
        Inventor.Application _invApp;
        Robot robo;
        bool _started = false;
        int precision = 8;
        double scale = 0.01;

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
                    ////MessageBox.Show(ex2.ToString());
                    ////MessageBox.Show("Unable to get or start Inventor");
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
                new SDF.Material("Red", new double[] { 255, 0, 0, 1.0 }));

            // Set the Collision attributes of the link.
            link1.Collision = new Collision(new SDF.Cylinder(1, 2));

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

            robot.WriteSDFToFile("C:\\Users\\W.Stott\\Documents\\SDF\\robo.xml");
            */

            #endregion

            //Test for inventor document, set some UI stuff.
            Reload();
        }

        public void Reload()
        {
            //Start robot
            try
            {
                robo = new Robot(_invApp.ActiveDocument.DisplayName);
            }
            catch (NullReferenceException ex)
            {
                this.label1.Text = "No inventor file found. Please open an assembly.";
                return;
            }

            this.label_output.Text = "SDF Model Name: " + robo.Name + "\n";

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

        public ComponentOccurrence[] GetCompOccurFromAss(AssemblyComponentDefinition asmCompDef) {
            return new ComponentOccurrence[asmCompDef.Occurrences.Count];
        }

        //Generate SDF file.
        private void GenerateSDF()
        {

            UnitsOfMeasure oUOM = _invApp.ActiveDocument.UnitsOfMeasure;
            AssemblyDocument oAsmDoc = (AssemblyDocument)_invApp.ActiveDocument;
            AssemblyComponentDefinition oAsmCompDef = oAsmDoc.ComponentDefinition;
            List<ComponentOccurrence> compOccurs = new List<ComponentOccurrence>();
            List<AssemblyConstraint> constraints = new List<AssemblyConstraint>();

            //Recursively make a list of all component parts.
            List<AssemblyComponentDefinition> loopList = new List<AssemblyComponentDefinition>();
            loopList.Add((AssemblyComponentDefinition)oAsmCompDef);
            while (loopList.Count > 0)
            {
                AssemblyComponentDefinition loopAsmCompDef = loopList[0];
                loopList.RemoveAt(0);
                foreach (ComponentOccurrence occ in loopAsmCompDef.Occurrences)
                {
                    if (occ.DefinitionDocumentType == DocumentTypeEnum.kPartDocumentObject || occ.Definition is WeldmentComponentDefinition)
                    {
                        //Store parts and weldments for link creation
                        compOccurs.Add(occ);
                        //occ.

                        WriteLine("Processing part '" + occ.Name + "' with " + occ.Constraints.Count + " constraints.");
                    }
                    else if (occ.DefinitionDocumentType == DocumentTypeEnum.kAssemblyDocumentObject)
                    {
                        //Get parts from assemblies.
                        loopList.Add((AssemblyComponentDefinition)occ.Definition);

                        WriteLine("Processing assembly '" + occ.Name + "' with " + occ.Constraints.Count + " constraints.");
                    }
                }

                //Get Assembly Constraints
                foreach (AssemblyConstraint cons in loopAsmCompDef.Constraints)
                {
                    constraints.Add(cons);
                }
            }

            WriteLine(compOccurs.Count.ToString() + " parts to convert.");
            WriteLine(constraints.Count.ToString() + " constraints to convert.");

            //Get all the available links
            foreach (ComponentOccurrence oCompOccur in compOccurs)
            {
                //Define a link
                Link link = new Link(RemoveColon(oCompOccur.Name));

                //Get global position and COM
                double Mass = oCompOccur.MassProperties.Mass;

                //Calculate rotation.
                Inventor.Matrix R1 = oCompOccur.Transformation;

                // Set position and rotation
                //TODO: Check globalised scale
                link.Pose = new Pose(R1, precision, scale);

                // Get Moments of Inertia.
                double[] iXYZ = new double[6];
                //Pos of C.O.M., Rot of Link.
                // Ixx, Iyy, Izz, Ixy, Iyz, Ixz
                // Ixx, Ixy, Ixz, Iyy, Iyz, Izz
                oCompOccur.MassProperties.XYZMomentsOfInertia(out iXYZ[0], out iXYZ[3], out iXYZ[5], out iXYZ[1], out iXYZ[4], out iXYZ[2]);
                Inventor.Point COMp = oCompOccur.MassProperties.CenterOfMass;
                double[] COM = new double[3] {COMp.X, COMp.Y, COMp.Z};

                // Set Moments of Inertia
                //Takes precision from link.Pose.
                link.Inertial = new Inertial(Mass, iXYZ, COM, link.Pose, scale);

                // Set the URI for the link's model.
                String URI = "model://<MODELNAME>/meshes/" + link.Name + ".stl";
                link.Visual = new Visual(new Mesh(URI, scale));
                link.Collision = new Collision(new Mesh(URI, scale));

                // Print out link information.
                WriteLine("New Link:          --------------------------------------");
                WriteLine("                  Name: " + link.Name);
                WriteLine("                  Pose: " + link.Pose.ToString());
                //link.Pose = new Pose(link.Pose);
                WriteLine("                  Pose: " + link.Pose.ToString());
                WriteLine("           InertiaPose: " + link.Inertial.Pose.ToString());
                Matrix<double> M2 = DenseMatrix.OfArray(link.Inertial.InertiaMatrix);
                WriteLine(M2.ToString());
                WriteLine("                  Mass: " + Mass);
                WriteLine("       Rotation Matrix: " + System.Environment.NewLine + link.Pose.PrintMatrix());

                // Add link to robot
                robo.Links.Add(link);

            }

            WriteLine(constraints.Count.ToString() + " constraints to convert.");

            //Get all the available joints
            foreach (AssemblyConstraint constraint in constraints) {
                //Some checks
                if (constraint.Suppressed) {
                    //Skip suppressed constraints.
                    WriteLine("Skipped a suppressed constraint.");
                    continue;
                }



                //Set some starting variables.
                String name = constraint.Name;
                Inventor.Point center;
                JointType type = JointType.Revolute;

                //Calculate which should be child.
                ComponentOccurrence childP = constraint.OccurrenceOne;
                ComponentOccurrence parentP = constraint.OccurrenceTwo;
                if (childP == null || parentP == null) {
                    //Skip incomplete constraints
                    WriteLine("Skipped a constraint without an Occurance.");
                    continue;
                }

                //Get degrees of freedom information.
                int transDOFCount, rotDOFCount;
                Inventor.ObjectsEnumerator transDOF, rotDOF;
                Inventor.Point DOFCenter;
                childP.GetDegreesOfFreedom(out transDOFCount, out transDOF, out rotDOFCount, out rotDOF, out DOFCenter);
                if (rotDOFCount + transDOFCount == 0)
                {
                    parentP.GetDegreesOfFreedom(out transDOFCount, out transDOF, out rotDOFCount, out rotDOF, out DOFCenter);
                    if (rotDOFCount + transDOFCount == 0)
                    {
                        WriteLine("Skipped a constraint with 0 DOF.");
                        continue;
                    }
                    //Parent has DOF but child doesn't. Switch Parent and Child.
                    ComponentOccurrence temp = childP;
                    childP = parentP;
                    parentP = temp;
                }

                //Get Model Links
                Link child = GetLinkByName(RemoveColon(childP.Name));
                Link parent = GetLinkByName(RemoveColon(parentP.Name));
                if (child == null || parent == null)
                {
                    //Skip incomplete constraints
                    WriteLine("Skipped a constraint without a Link.");
                    continue;
                }

                //Get constraint information
                Pose Pose;
                Axis Axis;
                Inventor.Point Geomcenter;
                if (constraint is InsertConstraint && constraint.GeometryOne is Circle)
                {
                    //Handle Insert Constraints (Revolute)
                    Circle circ = (Circle)constraint.GeometryOne;
                    Geomcenter = circ.Center;
                    Axis = new Axis(circ.Normal.X, circ.Normal.Y, circ.Normal.Z);
                    type = JointType.Revolute;
                }
                else if (constraint is MateConstraint && constraint.GeometryOne is Line)
                {
                    //Handle Mate Constraints (Prismatic)
                    Inventor.Line line = (Line)constraint.GeometryOne;
                    Geomcenter = line.RootPoint;
                    Axis = new Axis(line.Direction.X, line.Direction.Y, line.Direction.Z);
                    type = JointType.Prismatic;
                }
                else
                {
                    //Skip other constraints
                    WriteLine("Skipped an uknown constraint");
                    continue;
                }
                Pose = new Pose(Geomcenter.X, Geomcenter.Y, Geomcenter.Z, precision, scale);

                WriteLine("New joint:          --------------------------------------");
                WriteLine("                Name: " + name);
                WriteLine("              Parent: " + parent.Name);
                WriteLine("               Child: " + child.Name);
                WriteLine("                Pose: " + Pose.ToString());
                Pose.GetPosRelativeTo(child.Pose);
                WriteLine("           Child Loc: " + child.Pose.ToString());
                WriteLine("       Relative Pose: " + Pose.ToString());
                WriteLine("                Axis: " + Axis.ToString());
                WriteLine("              rotDOF: " + rotDOFCount + "    transDOF: " + transDOFCount);

                //Add the joint to the robot
                Joint joint = new Joint(name, type);
                joint.Axis = Axis;
                joint.Parent = parent;
                joint.Child = child;
                joint.Pose = Pose;
                robo.Joints.Add(joint);

            }

            //Saving turned on?
            if (this.checkBox2.Checked)
            {
                // Save the SDF
                FolderBrowserDialog folderDialog1 = new FolderBrowserDialog();
                if (folderDialog1.ShowDialog() == DialogResult.OK)
                {
                    robo.WriteSDFToFile(folderDialog1.SelectedPath, precision);
                }

                //Save STLs?
                foreach (ComponentOccurrence oCompOccur in compOccurs)
                {
                    if (oCompOccur.DefinitionDocumentType == DocumentTypeEnum.kPartDocumentObject && this.checkBox1.Checked)
                    {
                        PartDocument partDoc = (PartDocument)oCompOccur.Definition.Document;
                        partDoc.SaveAs(folderDialog1.SelectedPath + "\\meshes\\" + RemoveColon(oCompOccur.Name) + ".stl", true);
                        WriteLine("Finished saving: " + folderDialog1.SelectedPath + "\\meshes\\" + RemoveColon(oCompOccur.Name) + ".stl");
                    }
                }
            }

            #region oldcode
            //foreach (ComponentOccurrence oCompOccur in oAsmCompDef.Occurrences)
            //{
            //    // Generate links from available subassemblies in main assembly.
            //    //Debugger.Break();

            //    //New Link
            //    robo.Links.Add(new Link(oCompOccur.Name));
            //    int c = robo.Links.Count - 1;

            //    WriteLine("Added Link: "+ robo.Links[c].Name +", link count: " + robo.Links.Count.ToString());

            //    //Find and set parent link
            //    for (int i = 0; i < robo.Links.Count; i++)
            //    {
            //        if (String.Equals(robo.Links[i].Name, ReturnParentName(oCompOccur)))
            //        {
            //            robo.Links[c].Parent = robo.Links[i];
            //            WriteLine("Link's parent: " + robo.Links[i].Name);
            //        }
            //    }

                

            //    //If link has a parent
            //    if (robo.Links[c].Parent != null)
            //    {
            //        //Define a joint
            //        robo.Joints.Add(new Joint(FormatJointName(robo.Links[c].Name), JointType.Revolute, robo.Links[c].Parent, robo.Links[c]));
            //        int j = robo.Joints.Count - 1;

            //        //Parse joint axis
            //        switch (robo.Joints[j].Name[robo.Joints[j].Name.Length - 1])
            //        {
            //            case 'R':
            //                robo.Joints[j].Axis = new double[] { 1, 0, 0 };
            //                break;
            //            case 'P':
            //                robo.Joints[j].Axis = new double[] { 0, 1, 0 };
            //                break;
            //            case 'Y':
            //                robo.Joints[j].Axis = new double[] { 0, 0, 1 };
            //                break;
            //            default:
            //                break;
            //        }
            //    }

            //    // Get mass properties for each link.
            //    double[] iXYZ = new double[6];
            //    oCompOccur.MassProperties.XYZMomentsOfInertia(out iXYZ[0], out iXYZ[3], out iXYZ[5], out iXYZ[1], out iXYZ[4], out iXYZ[2]); // Ixx, Iyy, Izz, Ixy, Iyz, Ixz -> Ixx, Ixy, Ixz, Iyy, Iyz, Izz
            //    robo.Links[c].Inertial = new Inertial(oCompOccur.MassProperties.Mass, iXYZ);
            //    robo.Links[c].Inertial.XYZ = FindCenterOfMassOffset(oCompOccur);

            //    // Set shape properties for each link.
            //    robo.Links[c].Visual = new Visual(new Mesh("package://" + robo.Name + "/" + robo.Links[c].Name + ".stl"));
            //}
            #endregion

        }

        public string RemoveColon(String str)
        {
            return str.Replace(":", "-");
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

        public Inventor.Vector UpdateUnits(Inventor.Vector inp)
        {
            UnitsOfMeasure oUOM = _invApp.ActiveDocument.UnitsOfMeasure;

            inp.X = oUOM.ConvertUnits(inp.X, "cm", "m");
            inp.Y = oUOM.ConvertUnits(inp.Y, "cm", "m");
            inp.Z = oUOM.ConvertUnits(inp.Z, "cm", "m");

            return inp;
        }

        public Link GetLinkByName(String name)
        {
            Link link = null;

            foreach (Link l in robo.Links) {
                if (l.Name == name)
                {
                    link = l;
                }
            }

            return link;
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
            //Clear current SDF
            Reload();
            scale = Convert.ToDouble(this.textBox1.Text);
            //Generate
            this.buttonGen.Visible = false;
            GenerateSDF();
            this.buttonGen.Visible = true;
            //Set display
            Refresh();
        }

        private void buttonReload_Click(object sender, EventArgs e)
        {
            // Reload info, and reset SDF
            Reload();
        }

        private void WriteLine(String str)
        {
            this.label_output.Text += "\n";
            this.label_output.Text += str;
            this.panel1.VerticalScroll.Value = this.panel1.VerticalScroll.Maximum;
        }

    }
}
