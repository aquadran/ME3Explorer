﻿using System;
using System.Collections.Generic;
using System.Collections;
using System.Drawing;
using System.Drawing.Text;
using System.Linq;
using System.Windows.Forms;
using ME3Explorer.Unreal;
using ME3Explorer.Packages;

using UMD.HCIL.Piccolo;
using UMD.HCIL.Piccolo.Nodes;
using UMD.HCIL.Piccolo.Event;
using UMD.HCIL.Piccolo.Util;
using UMD.HCIL.GraphEditor;
using System.Runtime.InteropServices;
using ME1Explorer;

namespace ME3Explorer.SequenceObjects
{
    public enum VarTypes { Int, Bool, Object, Float, StrRef, MatineeData, Extern, String };

    public abstract class SObj : PNode
    {
        public IMEPackage pcc;
        public GraphEditor g;
        static readonly Color commentColor = Color.FromArgb(74, 63, 190);
        static readonly Color intColor = Color.FromArgb(34, 218, 218);//cyan
        static readonly Color floatColor = Color.FromArgb(23, 23, 213);//blue
        static readonly Color boolColor = Color.FromArgb(215, 37, 33); //red
        static readonly Color objectColor = Color.FromArgb(219, 39, 217);//purple
        static readonly Color interpDataColor = Color.FromArgb(222, 123, 26);//orange
        protected static Brush mostlyTransparentBrush = new SolidBrush(Color.FromArgb(1, 255, 255, 255));
        protected static Brush nodeBrush = new SolidBrush(Color.FromArgb(140, 140, 140));
        protected static Pen selectedPen = new Pen(Color.FromArgb(255, 255, 0));
        public static bool draggingOutlink = false;
        public static bool draggingVarlink = false;
        public static PNode dragTarget;
        public static bool OutputNumbers;

        public int Index => index;
        //public float Width { get { return shape.Width; } }
        //public float Height { get { return shape.Height; } }

        protected int index;
        protected IExportEntry export;
        protected Pen outlinePen;
        protected SText comment;

        protected SObj(int idx, IMEPackage p, GraphEditor grapheditor)
        {
            pcc = p;
            g = grapheditor;
            index = idx;
            export = pcc.getExport(index);
            comment = new SText(GetComment(), commentColor, false)
            {
                Pickable = false,
                X = 0
            };
            comment.Y = 0 - comment.Height;
            AddChild(comment);
            Pickable = true;
        }

        protected SObj(int idx, IMEPackage p)
        {
            pcc = p;
            index = idx;
            export = pcc.getExport(index);
            comment = new SText(GetComment(), commentColor, false);
            comment.X = 0;
            comment.Y = 0 - comment.Height;
            comment.Pickable = false;
            AddChild(comment);
            Pickable = true;
        }

        public virtual void CreateConnections(ref List<SObj> objects) { }
        public virtual void Layout(float x, float y) { }
        public virtual void Select() { }
        public virtual void Deselect() { }

        protected string GetComment()
        {
            string res = "";
            var comments = export.GetProperty<ArrayProperty<StrProperty>>("m_aObjComment");
            if (comments != null)
            {
                foreach (var s in comments)
                {
                    res += s + "\n";
                }
            }
            return res;
        }

        protected Color getColor(VarTypes t)
        {
            switch (t)
            {
                case VarTypes.Int:
                    return intColor;
                case VarTypes.Float:
                    return floatColor;
                case VarTypes.Bool:
                    return boolColor;
                case VarTypes.Object:
                    return objectColor;
                case VarTypes.MatineeData:
                    return interpDataColor;
                default:
                    return Color.Black;
            }
        }

        protected VarTypes getType(string s)
        {
            if (s.Contains("InterpData"))
                return VarTypes.MatineeData;
            if (s.Contains("Int"))
                return VarTypes.Int;
            if (s.Contains("Bool"))
                return VarTypes.Bool;
            if (s.Contains("Object") || s.Contains("Player"))
                return VarTypes.Object;
            if (s.Contains("Float"))
                return VarTypes.Float;
            if (s.Contains("StrRef"))
                return VarTypes.StrRef;
            if (s.Contains("String"))
                return VarTypes.String;
            return VarTypes.Extern;
        }
    }

    public class SVar : SObj
    {
        public VarTypes type { get; set; }
        readonly SText val;
        protected PPath shape;
        public string Value
        {
            get => val.Text;
            set => val.Text = value;
        }

        public SVar(int idx, float x, float y, IMEPackage p, GraphEditor grapheditor)
            : base(idx, p, grapheditor)
        {
            string s = export.ObjectName;
            s = s.Replace("BioSeqVar_", "");
            s = s.Replace("SFXSeqVar_", "");
            s = s.Replace("SeqVar_", "");
            type = getType(s);
            const float w = 60;
            const float h = 60;
            shape = PPath.CreateEllipse(0, 0, w, h);
            outlinePen = new Pen(getColor(type));
            shape.Pen = outlinePen;
            shape.Brush = nodeBrush;
            shape.Pickable = false;
            AddChild(shape);
            Bounds = new RectangleF(0, 0, w, h);
            val = new SText(GetValue());
            val.Pickable = false;
            val.TextAlignment = StringAlignment.Center;
            val.X = w / 2 - val.Width / 2;
            val.Y = h / 2 - val.Height / 2;
            AddChild(val);
            var props = export.GetProperties();
            foreach (var prop in props)
            {
                if ((prop.Name == "VarName" || prop.Name == "varName")
                    && prop is NameProperty nameProp)
                {
                    SText VarName = new SText(nameProp.Value, Color.Red, false)
                    {
                        Pickable = false,
                        TextAlignment = StringAlignment.Center,
                        Y = h
                    };
                    VarName.X = w / 2 - VarName.Width / 2;
                    AddChild(VarName);
                    break;
                }
            }
            this.TranslateBy(x, y);
            this.MouseEnter += OnMouseEnter;
            this.MouseLeave += OnMouseLeave;
        }

        public string GetValue()
        {
            try
            {
                var props = export.GetProperties();
                switch (type)
                {
                    case VarTypes.Int:
                        if (export.ObjectName == "BioSeqVar_StoryManagerInt")
                        {
                            if (props.GetProp<StrProperty>("m_sRefName") is StrProperty m_sRefName)
                            {
                                appendToComment(m_sRefName);
                            }
                            if (props.GetProp<IntProperty>("m_nIndex") is IntProperty m_nIndex)
                            {
                                return "Plot Int\n#" + m_nIndex.Value;
                            }
                        }
                        if (props.GetProp<IntProperty>("IntValue") is IntProperty intValue)
                        {
                            return intValue.Value.ToString();
                        }
                        return "0";
                    case VarTypes.Float:
                        if (export.ObjectName == "BioSeqVar_StoryManagerFloat")
                        {
                            if (props.GetProp<StrProperty>("m_sRefName") is StrProperty m_sRefName)
                            {
                                appendToComment(m_sRefName);
                            }
                            if (props.GetProp<IntProperty>("m_nIndex") is IntProperty m_nIndex)
                            {
                                return "Plot Float\n#" + m_nIndex.Value;
                            }
                        }
                        if (props.GetProp<FloatProperty>("FloatValue") is FloatProperty floatValue)
                        {
                            return floatValue.Value.ToString();
                        }
                        return "0.00";
                    case VarTypes.Bool:
                        if (export.ObjectName == "BioSeqVar_StoryManagerBool")
                        {
                            if (props.GetProp<StrProperty>("m_sRefName") is StrProperty m_sRefName)
                            {
                                appendToComment(m_sRefName);
                            }
                            if (props.GetProp<IntProperty>("m_nIndex") is IntProperty m_nIndex)
                            {
                                return "Plot Bool\n#" + m_nIndex.Value;
                            }
                        }
                        if (props.GetProp<IntProperty>("bValue") is IntProperty bValue)
                        {
                            return (bValue.Value == 1).ToString();
                        }
                        return "False";
                    case VarTypes.Object:
                        if (export.ObjectName == "SeqVar_Player")
                            return "Player";
                        foreach (var prop in props)
                        {
                            switch (prop)
                            {
                                case NameProperty nameProp when nameProp.Name == "m_sObjectTagToFind":
                                    return nameProp.Value;
                                case StrProperty strProp when strProp.Name == "m_sObjectTagToFind":
                                    return strProp.Value;
                                case ObjectProperty objProp when objProp.Name == "ObjValue":
                                    return pcc.getEntry(objProp.Value)?.ObjectName ?? "???";
                            }
                        }
                        return "???";
                    case VarTypes.StrRef:
                        foreach (var prop in props)
                        {
                            if ((prop.Name == "m_srValue" || prop.Name == "m_srStringID")
                                && prop is StringRefProperty strRefProp)
                            {
                                switch (pcc.Game)
                                {
                                    case MEGame.ME1:
                                        return ME1TalkFiles.findDataById(strRefProp.Value);
                                    case MEGame.ME2:
                                        return ME2Explorer.ME2TalkFiles.findDataById(strRefProp.Value);
                                    case MEGame.ME3:
                                        return ME3TalkFiles.findDataById(strRefProp.Value);
                                    case MEGame.UDK:
                                        return "UDK StrRef not supported";
                                }
                            }
                        }
                        return "???";
                    case VarTypes.String:
                        var strValue = props.GetProp<StrProperty>("StrValue");
                        if (strValue != null)
                        {
                            return strValue.Value;
                        }
                        return "???";
                    case VarTypes.Extern:
                        foreach (var prop in props)
                        {
                            switch (prop)
                            {
                                //Named Variable
                                case NameProperty nameProp when nameProp.Name == "FindVarName":
                                    return $"< {nameProp.Value} >";
                                //SeqVar_Name
                                case NameProperty nameProp when nameProp.Name == "NameValue":
                                    return nameProp.Value;
                                //External
                                case StrProperty strProp when strProp.Name == "VariableLabel":
                                    return $"Extern:\n{strProp.Value}";
                            }
                        }
                        return "???";
                    case VarTypes.MatineeData:
                        return $"#{index}\nInterpData";
                    default:
                        return "???";
                }
            }
            catch (Exception)
            {
                return "???";
            }
        }

        void appendToComment(string s)
        {
            if (comment.Text.Length > 0)
            {
                comment.TranslateBy(0, -1 * comment.Height);
                comment.Text += s + "\n";
            }
            else
            {
                comment.Text += s + "\n";
                comment.TranslateBy(0, -1 * comment.Height);
            }
        }

        public override void Select()
        {
            shape.Pen = selectedPen;
        }

        public override void Deselect()
        {
            shape.Pen = outlinePen;
        }

        public override bool Intersects(RectangleF bounds)
        {
            Region ellipseRegion = new Region(shape.PathReference);
            return ellipseRegion.IsVisible(bounds);
        }

        public void OnMouseEnter(object sender, PInputEventArgs e)
        {
            if (draggingVarlink)
            {
                ((PPath)((SVar)sender)[1]).Pen = selectedPen;
                dragTarget = (PNode)sender;
            }
        }

        public void OnMouseLeave(object sender, PInputEventArgs e)
        {
            if (draggingVarlink)
            {
                ((PPath)((SVar)sender)[1]).Pen = outlinePen;
                dragTarget = null;
            }
        }
    }

    public class SFrame : SObj
    {
        protected PPath shape;
        protected PPath titleBox;
        public SFrame(int idx, float x, float y, IMEPackage p, GraphEditor grapheditor)
            : base(idx, p, grapheditor)
        {
            string s = $"{export.ObjectName}_{export.indexValue}";
            float w = 0;
            float h = 0;
            var props = export.GetProperties();
            foreach (var prop in props)
            {
                if (prop.Name == "SizeX")
                {
                    w = (prop as IntProperty);
                }
                if (prop.Name == "SizeY")
                {
                    h = (prop as IntProperty);
                }
            }
            MakeTitleBox(s);
            shape = PPath.CreateRectangle(0, -titleBox.Height, w, h + titleBox.Height);
            outlinePen = new Pen(Color.Black);
            shape.Pen = outlinePen;
            shape.Brush = new SolidBrush(Color.Transparent);
            shape.Pickable = false;
            this.AddChild(shape);
            titleBox.TranslateBy(0, -titleBox.Height);
            this.AddChild(titleBox);
            comment.Y -= titleBox.Height;
            this.Bounds = new RectangleF(0, -titleBox.Height, titleBox.Width, titleBox.Height);
            this.TranslateBy(x, y);
        }

        protected void MakeTitleBox(string s)
        {
            s = "#" + index + " : " + s;
            SText title = new SText(s, Color.FromArgb(255, 255, 128))
            {
                TextAlignment = StringAlignment.Center,
                ConstrainWidthToTextWidth = false,
                X = 0,
                Y = 3,
                Pickable = false
            };
            title.Width += 20;
            titleBox = PPath.CreateRectangle(0, 0, title.Width, title.Height + 5);
            titleBox.Pen = outlinePen;
            titleBox.Brush = new SolidBrush(Color.FromArgb(112, 112, 112));
            titleBox.AddChild(title);
            titleBox.Pickable = false;
        }
    }

    public abstract class SBox : SObj
    {
        protected static Color titleBrush = Color.FromArgb(255, 255, 128);
        protected static Brush outputBrush = new SolidBrush(Color.Black);
        protected static Brush titleBoxBrush = new SolidBrush(Color.FromArgb(112, 112, 112));

        public struct OutputLink
        {
            public PPath node;
            public List<int> Links;
            public List<int> InputIndices;
            public string Desc;
        }

        public struct VarLink
        {
            public PPath node;
            public List<int> Links;
            public string Desc;
            public VarTypes type;
            public bool writeable;
            public List<int> offsets;
        }

        public struct InputLink
        {
            public PPath node;
            public string Desc;
            public int index;
            public bool hasName;
        }

        protected PPath titleBox;
        protected PPath varLinkBox;
        protected PPath outLinkBox;
        public List<OutputLink> Outlinks;
        public List<VarLink> Varlinks;

        protected SBox(int idx, IMEPackage p, GraphEditor grapheditor)
            : base(idx, p, grapheditor)
        {

        }

        protected SBox(int idx, IMEPackage p)
            : base(idx, p)
        {

        }

        public override void CreateConnections(ref List<SObj> objects)
        {
            foreach (OutputLink outLink in Outlinks)
            {
                for (int i = 0; i < objects.Count; i++)
                {
                    for (int j = 0; j < outLink.Links.Count; j++)
                    {
                        if (objects[i].Index == outLink.Links[j])
                        {
                            PPath p1 = outLink.node;
                            SObj p2 = (SObj)g.nodeLayer[i];
                            PPath edge = new PPath();
                            if (p1.Tag == null)
                                p1.Tag = new ArrayList();
                            if (p2.Tag == null)
                                p2.Tag = new ArrayList();
                            ((ArrayList)p1.Tag).Add(edge);
                            ((ArrayList)p2.Tag).Add(edge);
                            edge.Tag = new ArrayList();
                            ((ArrayList)edge.Tag).Add(p1);
                            ((ArrayList)edge.Tag).Add(p2);
                            ((ArrayList)edge.Tag).Add(outLink.InputIndices[j]);
                            g.addEdge(edge);
                        }
                    }
                }
            }
            foreach (VarLink v in Varlinks)
            {
                for (int i = 0; i < objects.Count; i++)
                {
                    foreach (int link in v.Links)
                    {
                        if (objects[i].Index == link)
                        {
                            PPath p1 = v.node;
                            PNode p2 = g.nodeLayer[i];
                            PPath edge = new PPath();
                            if (p1.Tag == null)
                                p1.Tag = new ArrayList();
                            if (p2.Tag == null)
                                p2.Tag = new ArrayList();
                            ((ArrayList)p1.Tag).Add(edge);
                            ((ArrayList)p2.Tag).Add(edge);
                            edge.Tag = new ArrayList();
                            if (p2.ChildrenCount > 1)
                                edge.Pen = ((PPath)p2[1]).Pen;
                            ((ArrayList)edge.Tag).Add(p1);
                            ((ArrayList)edge.Tag).Add(p2);
                            ((ArrayList)edge.Tag).Add(-1);//is a var link
                            g.addEdge(edge);
                        }
                    }
                }
            }
        }

        protected float GetTitleBox(string s, float w)
        {
            s = "#" + index + " : " + s;
            SText title = new SText(s, titleBrush)
            {
                TextAlignment = StringAlignment.Center,
                ConstrainWidthToTextWidth = false,
                X = 0,
                Y = 3,
                Pickable = false
            };
            if (title.Width + 20 > w)
            {
                w = title.Width + 20;
            }
            title.Width = w;
            titleBox = PPath.CreateRectangle(0, 0, w, title.Height + 5);
            titleBox.Pen = outlinePen;
            titleBox.Brush = titleBoxBrush;
            titleBox.AddChild(title);
            titleBox.Pickable = false;
            return w;
        }

        protected void GetVarLinks()
        {
            Varlinks = new List<VarLink>();
            var varLinksProp = export.GetProperty<ArrayProperty<StructProperty>>("VariableLinks");
            if (varLinksProp != null)
            {
                foreach (var prop in varLinksProp)
                {
                    PropertyCollection props = prop.Properties;
                    var linkedVars = props.GetProp<ArrayProperty<ObjectProperty>>("LinkedVariables");
                    if (linkedVars != null)
                    {
                        VarLink l = new VarLink
                        {
                            Links = new List<int>(),
                            Desc = props.GetProp<StrProperty>("LinkDesc")
                        };
                        if (linkedVars.Count != 0)
                        {
                            foreach (var objProp in linkedVars)
                            {
                                l.Links.Add(objProp.Value - 1);
                            }
                        }
                        else
                        {
                            l.Links.Add(-1);
                        }
                        l.type = getType(pcc.getObjectName(props.GetProp<ObjectProperty>("ExpectedType").Value));
                        l.writeable = props.GetProp<BoolProperty>("bWriteable").Value;
                        if (l.writeable)
                        {
                            //downward pointing triangle
                            l.node = PPath.CreatePolygon(new[] { new PointF(-4, 0), new PointF(4, 0), new PointF(0, 10) });
                            l.node.AddChild(PPath.CreatePolygon(new[] { new PointF(-4, 0), new PointF(4, 0), new PointF(0, 10) }));
                        }
                        else
                        {
                            l.node = PPath.CreateRectangle(-4, 0, 8, 10);
                            l.node.AddChild(PPath.CreateRectangle(-4, 0, 8, 10));
                        }
                        l.node.Brush = new SolidBrush(getColor(l.type));
                        l.node.Pen = new Pen(getColor(l.type));
                        l.node.Pickable = false;
                        l.node[0].Brush = mostlyTransparentBrush;
                        ((PPath)l.node[0]).Pen = l.node.Pen;
                        l.node[0].X = l.node.X;
                        l.node[0].Y = l.node.Y;
                        l.node[0].AddInputEventListener(new VarDragHandler());
                        Varlinks.Add(l);
                    }
                }
            }
        }

        protected void GetOutputLinks()
        {
            Outlinks = new List<OutputLink>();
            var outLinksProp = export.GetProperty<ArrayProperty<StructProperty>>("OutputLinks");
            if (outLinksProp != null)
            {
                foreach (var prop in outLinksProp)
                {
                    PropertyCollection props = prop.Properties;
                    var linksProp = props.GetProp<ArrayProperty<StructProperty>>("Links");
                    if (linksProp != null)
                    {
                        OutputLink l = new OutputLink
                        {
                            Links = new List<int>(),
                            InputIndices = new List<int>(),
                            Desc = props.GetProp<StrProperty>("LinkDesc")
                        };
                        if (linksProp.Count != 0)
                        {
                            for (int i = 0; i < linksProp.Count; i += 1)
                            {
                                int linkedOp = linksProp[i].GetProp<ObjectProperty>("LinkedOp").Value - 1;
                                l.Links.Add(linkedOp);
                                l.InputIndices.Add(linksProp[i].GetProp<IntProperty>("InputLinkIdx"));
                                if (OutputNumbers)
                                    l.Desc = l.Desc + (i > 0 ? "," : ": ") + "#" + (linkedOp);
                            }
                        }
                        else
                        {
                            l.Links.Add(-1);
                            l.InputIndices.Add(0);
                            if (OutputNumbers)
                                l.Desc = l.Desc + ": #-1";
                        }
                        l.node = PPath.CreateRectangle(0, -4, 10, 8);
                        l.node.Brush = outputBrush;
                        l.node.Pickable = false;
                        l.node.AddChild(PPath.CreateRectangle(0, -4, 10, 8));
                        l.node[0].Brush = mostlyTransparentBrush;
                        l.node[0].X = l.node.X;
                        l.node[0].Y = l.node.Y;
                        l.node[0].AddInputEventListener(new OutputDragHandler());
                        Outlinks.Add(l);
                    }
                }
            }
        }

        protected class OutputDragHandler : PDragEventHandler
        {
            public override bool DoesAcceptEvent(PInputEventArgs e)
            {
                return e.IsMouseEvent && (e.Button != MouseButtons.None || e.IsMouseEnterOrMouseLeave) && !e.Handled;
            }

            protected override void OnStartDrag(object sender, PInputEventArgs e)
            {
                e.PickedNode.Parent.Parent.Parent.Parent.MoveToBack();
                e.Handled = true;
                PNode p1 = ((PNode)sender).Parent;
                PNode p2 = (PNode)sender;
                PPath edge = new PPath();
                if (p1.Tag == null)
                    p1.Tag = new ArrayList();
                if (p2.Tag == null)
                    p2.Tag = new ArrayList();
                ((ArrayList)p1.Tag).Add(edge);
                ((ArrayList)p2.Tag).Add(edge);
                edge.Tag = new ArrayList();
                ((ArrayList)edge.Tag).Add(p1);
                ((ArrayList)edge.Tag).Add(p2);
                ((SBox)e.PickedNode.Parent.Parent.Parent.Parent).g.addEdge(edge);
                base.OnStartDrag(sender, e);
                draggingOutlink = true;
            }

            protected override void OnDrag(object sender, PInputEventArgs e)
            {
                base.OnDrag(sender, e);
                e.Handled = true;
                GraphEditor.UpdateEdge((PPath)((ArrayList)((PNode)sender).Tag)[0]);
            }

            protected override void OnEndDrag(object sender, PInputEventArgs e)
            {
                PPath edge = (PPath)((ArrayList)((PNode)sender).Tag)[0];
                ((PNode)sender).SetOffset(0, 0);
                ((ArrayList)((PNode)sender).Parent.Tag).Remove(edge);
                ((SBox)e.PickedNode.Parent.Parent.Parent.Parent).g.edgeLayer.RemoveChild(edge);
                ((ArrayList)((PNode)sender).Tag).RemoveAt(0);
                base.OnEndDrag(sender, e);
                draggingOutlink = false;
                if (dragTarget != null)
                {
                    ((SBox)e.PickedNode.Parent.Parent.Parent.Parent).CreateOutlink(((PPath)sender).Parent, dragTarget);
                }
            }
        }

        protected class VarDragHandler : PDragEventHandler
        {

            public override bool DoesAcceptEvent(PInputEventArgs e)
            {
                return e.IsMouseEvent && (e.Button != MouseButtons.None || e.IsMouseEnterOrMouseLeave) && !e.Handled;
            }

            protected override void OnStartDrag(object sender, PInputEventArgs e)
            {
                e.PickedNode.Parent.Parent.Parent.Parent.MoveToBack();
                e.Handled = true;
                PNode p1 = ((PNode)sender).Parent;
                PNode p2 = (PNode)sender;
                PPath edge = new PPath();
                if (p1.Tag == null)
                    p1.Tag = new ArrayList();
                if (p2.Tag == null)
                    p2.Tag = new ArrayList();
                ((ArrayList)p1.Tag).Add(edge);
                ((ArrayList)p2.Tag).Add(edge);
                edge.Tag = new ArrayList();
                ((ArrayList)edge.Tag).Add(p1);
                ((ArrayList)edge.Tag).Add(p2);
                ((ArrayList)edge.Tag).Add(-1);
                ((SBox)e.PickedNode.Parent.Parent.Parent.Parent).g.addEdge(edge);
                base.OnStartDrag(sender, e);
                draggingVarlink = true;

            }

            protected override void OnDrag(object sender, PInputEventArgs e)
            {
                base.OnDrag(sender, e);
                e.Handled = true;
                GraphEditor.UpdateEdge((PPath)((ArrayList)((PNode)sender).Tag)[0]);
            }

            protected override void OnEndDrag(object sender, PInputEventArgs e)
            {
                PPath edge = (PPath)((ArrayList)((PNode)sender).Tag)[0];
                ((PNode)sender).SetOffset(0, 0);
                ((ArrayList)((PNode)sender).Parent.Tag).Remove(edge);
                ((SBox)e.PickedNode.Parent.Parent.Parent.Parent).g.edgeLayer.RemoveChild(edge);
                ((ArrayList)((PNode)sender).Tag).RemoveAt(0);
                base.OnEndDrag(sender, e);
                draggingVarlink = false;
                if (dragTarget != null)
                {
                    ((SBox)e.PickedNode.Parent.Parent.Parent.Parent).CreateVarlink(((PPath)sender).Parent, (SVar)dragTarget);
                }
            }
        }

        public void CreateOutlink(PNode n1, PNode n2)
        {
            SBox start = (SBox)n1.Parent.Parent.Parent;
            SAction end = (SAction)n2.Parent.Parent.Parent;
            IExportEntry startExport = pcc.getExport(start.Index);
            string linkDesc = null;
            foreach (OutputLink l in start.Outlinks)
            {
                if (l.node == n1)
                {
                    if (l.Links.Contains(end.Index))
                        return;
                    linkDesc = l.Desc;
                    break;
                }
            }
            if (linkDesc == null)
                return;
            linkDesc = OutputNumbers ? linkDesc.Substring(0, linkDesc.LastIndexOf(":")) : linkDesc;
            int inputIndex = -1;
            foreach (InputLink l in end.InLinks)
            {
                if (l.node == n2)
                {
                    inputIndex = l.index;
                }
            }
            if (inputIndex == -1)
                return;
            var outLinksProp = startExport.GetProperty<ArrayProperty<StructProperty>>("OutputLinks");
            if (outLinksProp != null)
            {
                foreach (var prop in outLinksProp)
                {
                    if (prop.GetProp<StrProperty>("LinkDesc") == linkDesc)
                    {
                        var linksProp = prop.GetProp<ArrayProperty<StructProperty>>("Links");
                        linksProp.Add(new StructProperty("SeqOpOutputInputLink", false,
                            new ObjectProperty(end.index + 1, "LinkedOp"),
                            new IntProperty(inputIndex, "InputLinkIdx")));
                        startExport.WriteProperty(outLinksProp);
                        return;
                    }
                }
            }
        }

        public void CreateVarlink(PNode p1, SVar end)
        {
            SBox start = (SBox)p1.Parent.Parent.Parent;
            IExportEntry startExport = pcc.getExport(start.Index);
            string linkDesc = null;
            foreach (VarLink l in start.Varlinks)
            {
                if (l.node == p1)
                {
                    if (l.Links.Contains(end.Index))
                        return;
                    linkDesc = l.Desc;
                    break;
                }
            }
            if (linkDesc == null)
                return;
            var varLinksProp = startExport.GetProperty<ArrayProperty<StructProperty>>("VariableLinks");
            if (varLinksProp != null)
            {
                foreach (var prop in varLinksProp)
                {
                    if (prop.GetProp<StrProperty>("LinkDesc") == linkDesc)
                    {
                        prop.GetProp<ArrayProperty<ObjectProperty>>("LinkedVariables").Add(new ObjectProperty(end.Index + 1));
                        startExport.WriteProperty(varLinksProp);
                    }
                }
            }
        }

        public void RemoveOutlink(int linkconnection, int linkIndex)
        {
            string linkDesc = Outlinks[linkconnection].Desc;
            linkDesc = (OutputNumbers ? linkDesc.Substring(0, linkDesc.LastIndexOf(":")) : linkDesc);
            var outLinksProp = export.GetProperty<ArrayProperty<StructProperty>>("OutputLinks");
            if (outLinksProp != null)
            {
                foreach (var prop in outLinksProp)
                {
                    if (prop.GetProp<StrProperty>("LinkDesc") == linkDesc)
                    {
                        prop.GetProp<ArrayProperty<StructProperty>>("Links").RemoveAt(linkIndex);
                        export.WriteProperty(outLinksProp);
                        return;
                    }
                }
            }
        }

        public void RemoveVarlink(int linkconnection, int linkIndex)
        {
            string linkDesc = Varlinks[linkconnection].Desc;
            var varLinksProp = export.GetProperty<ArrayProperty<StructProperty>>("VariableLinks");
            if (varLinksProp != null)
            {
                foreach (var prop in varLinksProp)
                {
                    if (prop.GetProp<StrProperty>("LinkDesc") == linkDesc)
                    {
                        prop.GetProp<ArrayProperty<ObjectProperty>>("LinkedVariables").RemoveAt(linkIndex);
                        export.WriteProperty(varLinksProp);
                        return;
                    }
                }
            }
        }
    }

    public class SEvent : SBox
    {
        public SEvent(int idx, float x, float y, IMEPackage p, GraphEditor grapheditor)
            : base(idx, p, grapheditor)
        {
            outlinePen = new Pen(Color.FromArgb(214, 30, 28));
            string s = export.ObjectName;
            s = s.Replace("BioSeqEvt_", "");
            s = s.Replace("SFXSeqEvt_", "");
            s = s.Replace("SeqEvt_", "");
            s = s.Replace("SeqEvent_", "");
            s += "_" + export.indexValue;
            float starty = 0;
            float w = 15;
            float midW = 0;
            varLinkBox = new PPath();
            GetVarLinks();
            for (int i = 0; i < Varlinks.Count; i++)
            {
                string d = "";
                foreach (int l in Varlinks[i].Links)
                    d = d + "#" + l + ",";
                d = d.Remove(d.Length - 1);
                SText t2 = new SText(d + "\n" + Varlinks[i].Desc)
                {
                    X = w,
                    Y = 0,
                    Pickable = false
                };
                w += t2.Width + 20;
                Varlinks[i].node.TranslateBy(t2.X + t2.Width / 2, t2.Y + t2.Height);
                t2.AddChild(Varlinks[i].node);
                varLinkBox.AddChild(t2);
            }
            if (Varlinks.Count != 0)
                varLinkBox.AddRectangle(0, 0, w, varLinkBox[0].Height);
            varLinkBox.Pickable = false;
            varLinkBox.Pen = outlinePen;
            varLinkBox.Brush = nodeBrush;
            GetOutputLinks();
            outLinkBox = new PPath();
            for (int i = 0; i < Outlinks.Count; i++)
            {
                SText t2 = new SText(Outlinks[i].Desc);
                if (t2.Width + 10 > midW) midW = t2.Width + 10;
                //t2.TextAlignment = StringAlignment.Far;
                //t2.ConstrainWidthToTextWidth = false;
                t2.X = 0 - t2.Width;
                t2.Y = starty + 3;
                starty += t2.Height + 6;
                t2.Pickable = false;
                Outlinks[i].node.TranslateBy(0, t2.Y + t2.Height / 2);
                t2.AddChild(Outlinks[i].node);
                outLinkBox.AddChild(t2);
            }
            outLinkBox.AddPolygon(new[] { new PointF(0, 0), new PointF(0, starty), new PointF(-0.5f * midW, starty + 30), new PointF(0 - midW, starty), new PointF(0 - midW, 0), new PointF(midW / -2, -30) });
            outLinkBox.Pickable = false;
            outLinkBox.Pen = outlinePen;
            outLinkBox.Brush = nodeBrush;
            var props = export.GetProperties();
            foreach (var prop in props)
            {
                if (prop.Name.Name.Contains("EventName") || prop.Name == "sScriptName")
                    s += "\n\"" + (prop as NameProperty) + "\"";
                else if (prop.Name == "InputLabel" || prop.Name == "sEvent")
                    s += "\n\"" + (prop as StrProperty) + "\"";
            }
            float tW = GetTitleBox(s, w);
            if (tW > w)
            {
                if (midW > tW)
                {
                    w = midW;
                    titleBox.Width = w;
                }
                else
                {
                    w = tW;
                }
                varLinkBox.Width = w;
            }
            float h = titleBox.Height + 1;
            outLinkBox.TranslateBy(titleBox.Width / 2 + midW / 2, h + 30);
            h += outLinkBox.Height + 1;
            varLinkBox.TranslateBy(0, h);
            h += varLinkBox.Height;
            this.bounds = new RectangleF(0, 0, w, h);
            this.AddChild(titleBox);
            this.AddChild(varLinkBox);
            this.AddChild(outLinkBox);
            this.TranslateBy(x, y);
        }

        public override void Select()
        {
            titleBox.Pen = selectedPen;
            varLinkBox.Pen = selectedPen;
            outLinkBox.Pen = selectedPen;
        }

        public override void Deselect()
        {
            titleBox.Pen = outlinePen;
            varLinkBox.Pen = outlinePen;
            outLinkBox.Pen = outlinePen;
        }

        //public override bool Intersects(RectangleF bounds)
        //{
        //    Region hitregion = new Region(outLinkBox.PathReference);
        //    //hitregion.Union(titleBox.PathReference);
        //    //hitregion.Union(varLinkBox.PathReference);
        //    return hitregion.IsVisible(bounds);
        //}
    }

    public class SAction : SBox
    {
        public List<InputLink> InLinks;
        protected PNode inputLinkBox;
        protected PPath box;
        protected float originalX;
        protected float originalY;

        public SAction(int idx, float x, float y, IMEPackage p, GraphEditor grapheditor)
            : base(idx, p, grapheditor)
        {
            GetVarLinks();
            GetOutputLinks();
            originalX = x;
            originalY = y;
        }

        public SAction(int idx, float x, float y, IMEPackage p)
            : base(idx, p)
        {
            GetVarLinks();
            GetOutputLinks();
            originalX = x;
            originalY = y;
        }
        public override void Select()
        {
            titleBox.Pen = selectedPen;
            ((PPath)this[1]).Pen = selectedPen;
        }

        public override void Deselect()
        {
            titleBox.Pen = outlinePen;
            ((PPath)this[1]).Pen = outlinePen;
        }

        public override void Layout(float x, float y)
        {
            if (pcc.Game == MEGame.ME1)
            {
                // ==
                if (Math.Abs(x - -0.1f) < float.Epsilon)
                    x = originalX;
                // ==
                if (Math.Abs(y - -0.1f) < float.Epsilon)
                    y = originalY;
            }
            else
            {
                // ==
                if (Math.Abs(originalX - -1) > float.Epsilon)
                    x = originalX;
                // ==
                if (Math.Abs(originalY - -1) > float.Epsilon)
                    y = originalY;
            }
            outlinePen = new Pen(Color.Black);
            string s = export.ObjectName;
            s = s.Replace("BioSeqAct_", "");
            s = s.Replace("SFXSeqAct_", "");
            s = s.Replace("SeqAct_", "");
            s = s.Replace("SeqCond_", "");
            float starty = 8;
            float w = 20;
            varLinkBox = new PPath();
            for (int i = 0; i < Varlinks.Count; i++)
            {
                string d = "";
                foreach (int l in Varlinks[i].Links)
                    d = d + "#" + l + ",";
                d = d.Remove(d.Length - 1);
                SText t2 = new SText(d + "\n" + Varlinks[i].Desc)
                {
                    X = w,
                    Y = 0,
                    Pickable = false
                };
                w += t2.Width + 20;
                Varlinks[i].node.TranslateBy(t2.X + t2.Width / 2, t2.Y + t2.Height);
                t2.AddChild(Varlinks[i].node);
                varLinkBox.AddChild(t2);
            }
            if (Varlinks.Count != 0)
                varLinkBox.Height = varLinkBox[0].Height;
            varLinkBox.Width = w;
            varLinkBox.Pickable = false;
            outLinkBox = new PPath();
            float outW = 0;
            for (int i = 0; i < Outlinks.Count; i++)
            {
                SText t2 = new SText(Outlinks[i].Desc);
                if (t2.Width + 10 > outW) outW = t2.Width + 10;
                t2.X = 0 - t2.Width;
                t2.Y = starty;
                starty += t2.Height;
                t2.Pickable = false;
                Outlinks[i].node.TranslateBy(0, t2.Y + t2.Height / 2);
                t2.AddChild(Outlinks[i].node);
                outLinkBox.AddChild(t2);
            }
            outLinkBox.Pickable = false;
            inputLinkBox = new PNode();
            GetInputLinks();
            float inW = 0;
            float inY = 8;
            for (int i = 0; i < InLinks.Count; i++)
            {
                SText t2 = new SText(InLinks[i].Desc);
                if (t2.Width > inW) inW = t2.Width;
                t2.X = 3;
                t2.Y = inY;
                inY += t2.Height;
                t2.Pickable = false;
                InLinks[i].node.X = -10;
                InLinks[i].node.Y = t2.Y + t2.Height / 2 - 5;
                t2.AddChild(InLinks[i].node);
                inputLinkBox.AddChild(t2);
            }
            inputLinkBox.Pickable = false;
            if (inY > starty) starty = inY;
            if (inW + outW + 10 > w) w = inW + outW + 10;
            foreach (var prop in export.GetProperties())
            {
                switch (prop)
                {
                    case ObjectProperty objProp when objProp.Name == "oSequenceReference":
                        {
                            string seqName = pcc.getEntry(objProp.Value).ObjectName;
                            if (pcc.Game == MEGame.ME1
                                && objProp.Value > 0
                                && seqName == "Sequence"
                                && pcc.getExport(objProp.Value - 1).GetProperty<StrProperty>("ObjName") is StrProperty objNameProp)
                            {
                                seqName = objNameProp;
                            }
                            s += $"\n\"{seqName}\"";
                            break;
                        }
                    case NameProperty nameProp when nameProp.Name == "EventName" || nameProp.Name == "StateName":
                        s += $"\n\"{nameProp}\"";
                        break;
                    case StrProperty strProp when strProp.Name == "OutputLabel" || strProp.Name == "m_sMovieName":
                        s += $"\n\"{strProp}\"";
                        break;
                    case ObjectProperty objProp when objProp.Name == "m_pEffect":
                        s += $"\n\"{pcc.getEntry(objProp.Value).ObjectName}\"";
                        break;
                }
            }
            float tW = GetTitleBox(s, w);
            if (tW > w)
            {
                w = tW;
                titleBox.Width = w;
            }
            titleBox.X = 0;
            titleBox.Y = 0;
            float h = titleBox.Height + 2;
            inputLinkBox.TranslateBy(0, h);
            outLinkBox.TranslateBy(w, h);
            h += starty + 8;
            varLinkBox.TranslateBy(varLinkBox.Width < w ? (w - varLinkBox.Width) / 2 : 0, h);
            h += varLinkBox.Height;
            box = PPath.CreateRectangle(0, titleBox.Height + 2, w, h - (titleBox.Height + 2));
            box.Brush = nodeBrush;
            box.Pen = outlinePen;
            box.Pickable = false;
            this.Bounds = new RectangleF(0, 0, w, h);
            this.AddChild(box);
            this.AddChild(titleBox);
            this.AddChild(varLinkBox);
            this.AddChild(outLinkBox);
            this.AddChild(inputLinkBox);
            this.TranslateBy(x, y);
        }

        private void GetInputLinks()
        {
            InLinks = new List<InputLink>();
            var inputLinksProp = export.GetProperty<ArrayProperty<StructProperty>>("InputLinks");
            if (export.ClassName == "SequenceReference")
            {
                var oSequenceReference = export.GetProperty<ObjectProperty>("oSequenceReference");
                if (oSequenceReference != null)
                {
                    inputLinksProp = pcc.getExport(oSequenceReference.Value - 1).GetProperty<ArrayProperty<StructProperty>>("InputLinks");
                }
            }
            if (inputLinksProp != null)
            {
                for (int i = 0; i < inputLinksProp.Count; i++)
                {
                    InputLink l = new InputLink
                    {
                        Desc = inputLinksProp[i].GetProp<StrProperty>("LinkDesc"),
                        hasName = true,
                        index = i,
                        node = PPath.CreateRectangle(0, -4, 10, 8)
                    };
                    l.node.Brush = outputBrush;
                    l.node.MouseEnter += OnMouseEnter;
                    l.node.MouseLeave += OnMouseLeave;
                    l.node.AddInputEventListener(new InputDragHandler());
                    InLinks.Add(l);
                }
            }
            else if (pcc.Game == MEGame.ME3)
            {
                try
                {
                    if (ME3UnrealObjectInfo.getSequenceObjectInfo(export.ClassName)?.inputLinks is List<string> inputLinks)
                    {
                        for (int i = 0; i < inputLinks.Count; i++)
                        {
                            InputLink l = new InputLink
                            {
                                Desc = inputLinks[i],
                                hasName = true,
                                index = i,
                                node = PPath.CreateRectangle(0, -4, 10, 8)
                            };
                            l.node.Brush = outputBrush;
                            l.node.MouseEnter += OnMouseEnter;
                            l.node.MouseLeave += OnMouseLeave;
                            l.node.AddInputEventListener(new InputDragHandler());
                            InLinks.Add(l);
                        }
                    }
                }
                catch (Exception)
                {
                    InLinks.Clear();
                }
            }
            if (this.Tag != null)
            {
                int numInputs = InLinks.Count;
                foreach (PPath edge in ((ArrayList)this.Tag))
                {
                    int inputNum = (int)((ArrayList)edge.Tag)[2];
                    //if there are inputs with an index greater than is accounted for by
                    //the current number of inputs, create enough inputs to fill up to that index
                    if (inputNum + 1 > numInputs)
                    {
                        for (int i = numInputs; i <= inputNum; i++)
                        {
                            InputLink l = new InputLink
                            {
                                Desc = ":" + i,
                                hasName = false,
                                index = i,
                                node = PPath.CreateRectangle(0, -4, 10, 8)
                            };
                            l.node.Brush = outputBrush;
                            l.node.MouseEnter += OnMouseEnter;
                            l.node.MouseLeave += OnMouseLeave;
                            l.node.AddInputEventListener(new InputDragHandler());
                            InLinks.Add(l);
                        }
                        numInputs = inputNum + 1;
                    }
                    if (inputNum >= 0)
                        ((ArrayList)edge.Tag)[1] = InLinks[inputNum].node;
                }
            }
        }

        public class InputDragHandler : PDragEventHandler
        {
            public override bool DoesAcceptEvent(PInputEventArgs e)
            {
                return e.IsMouseEvent && (e.Button != MouseButtons.None || e.IsMouseEnterOrMouseLeave) && !e.Handled;
            }

            protected override void OnStartDrag(object sender, PInputEventArgs e)
            {
                e.Handled = true;
            }

            protected override void OnDrag(object sender, PInputEventArgs e)
            {
                e.Handled = true;
            }

            protected override void OnEndDrag(object sender, PInputEventArgs e)
            {
                e.Handled = true;
            }
        }

        public void OnMouseEnter(object sender, PInputEventArgs e)
        {
            if (draggingOutlink)
            {
                ((PPath)sender).Pen = selectedPen;
                dragTarget = (PPath)sender;
            }
        }

        public void OnMouseLeave(object sender, PInputEventArgs e)
        {
            ((PPath)sender).Pen = outlinePen;
            dragTarget = null;
        }

    }

    public class SText : PText
    {
        [DllImport("gdi32.dll")]
        private static extern IntPtr AddFontMemResourceEx(IntPtr pbFont, uint cbFont, IntPtr pdv, [In] ref uint pcFonts);

        private readonly Brush black = new SolidBrush(Color.Black);
        public bool shadowRendering { get; set; }
        private static PrivateFontCollection fontcollection;
        private static Font kismetFont;

        public SText(string s, bool shadows = true)
            : base(s)
        {
            base.TextBrush = new SolidBrush(Color.FromArgb(255, 255, 255));
            base.Font = kismetFont;

            shadowRendering = shadows;
        }

        public SText(string s, Color c, bool shadows = true)
            : base(s)
        {
            base.TextBrush = new SolidBrush(c);
            base.Font = kismetFont;
            shadowRendering = shadows;
        }

        //must be called once in the program before SText can be used
        public static void LoadFont()
        {
            if (fontcollection == null || fontcollection.Families.Length < 1)
            {
                fontcollection = new PrivateFontCollection();
                byte[] fontData = Properties.Resources.KismetFont;
                IntPtr fontPtr = Marshal.AllocCoTaskMem(fontData.Length);
                Marshal.Copy(fontData, 0, fontPtr, fontData.Length);
                fontcollection.AddMemoryFont(fontPtr, fontData.Length);
                uint tmp = 0;
                AddFontMemResourceEx(fontPtr, (uint)(fontData.Length), IntPtr.Zero, ref tmp);
                Marshal.FreeCoTaskMem(fontPtr);
                kismetFont = new Font(fontcollection.Families[0], 6);
            }
        }

        protected override void Paint(PPaintContext paintContext)
        {
            paintContext.Graphics.TextRenderingHint = TextRenderingHint.SingleBitPerPixel;
            //paints dropshadow
            if (shadowRendering && paintContext.Scale >= 1 && base.Text != null && base.TextBrush != null && base.Font != null)
            {
                Graphics g = paintContext.Graphics;
                float renderedFontSize = base.Font.SizeInPoints * paintContext.Scale;
                if (renderedFontSize >= PUtil.GreekThreshold && renderedFontSize < PUtil.MaxFontSize)
                {
                    RectangleF shadowbounds = Bounds;
                    shadowbounds.Offset(1, 1);
                    StringFormat stringformat = new StringFormat { Alignment = base.TextAlignment };
                    g.DrawString(base.Text, base.Font, black, shadowbounds, stringformat);
                }
            }
            base.Paint(paintContext);
        }
    }
}