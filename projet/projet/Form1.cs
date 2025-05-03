using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace projet
{
    public partial class Form1 : Form
    {
        private List<NodeDetails> nodeDetailsList;
        private Tree arbre;
        private Dictionary<string, Tree> treeNodeLookup = new Dictionary<string, Tree>();
        private Dictionary<RectangleF, string> nodeHitboxes = new Dictionary<RectangleF, string>();

        
        private float zoom = 1.0f;
        private float panX = 0;
        private float panY = 0;
        private bool isDragging = false;
        private Point lastMousePosition;

        
        private string selectedNodeId = null;
        private HashSet<string> selectedSubtree = new HashSet<string>();

        private readonly string treePath = "treeoflife_links.csv";
        private readonly string nodePath = "treeoflife_nodes.csv";

        public Form1(){
            InitializeComponent();
            this.DoubleBuffered = true; 
            this.Paint += Form1_Paint;
            this.MouseWheel += Form1_MouseWheel;
            this.MouseDown += Form1_MouseDown;
            this.MouseMove += Form1_MouseMove;
            this.MouseUp += Form1_MouseUp;
            this.MouseClick += Form1_MouseClick;

            loadData();
        }

        public class TreeNode {
            public string Parent, Child;
        }

        public class NodeDetails{
            public string ID, Name, Description;
            public int Extinct, Confidence, Phylesis;
        }

        public class Tree{
            public string Node;
            public List<Tree> Children = new List<Tree>();
            public int SubtreeSize; 
        }

        private void loadData(){

            if (!File.Exists(treePath) || !File.Exists(nodePath)){
                MessageBox.Show("CSV files not found.");
                return;
            }

            nodeDetailsList = loadNodeDetails(nodePath);
            var links = loadTreeNodes(treePath);
            var racine = links.Select(n => n.Parent).Except(links.Select(n => n.Child)).FirstOrDefault();

            if (racine == null){
                MessageBox.Show("Root node not found.");
                return;
            }

            arbre = buildTree(racine, links);
            calculTaillSousArbre(arbre);
             
        }

        public List<TreeNode> loadTreeNodes(string path) =>
            File.ReadAllLines(path).Skip(1)
                .Select(l => l.Split(','))
                .Where(p => p.Length >= 2)
                .Select(p => new TreeNode { Parent = p[0].Trim(), Child = p[1].Trim() })
                .ToList();

        public List<NodeDetails> loadNodeDetails(string path) =>
            File.ReadAllLines(path).Skip(1)
                .Select(l => l.Split(','))
                .Where(p => p.Length >= 8)
                .Select(p => new NodeDetails
                {
                    ID = p[0].Trim(),
                    Name = p[1].Trim(),
                    Description = p[2].Trim(),
                    Extinct = int.TryParse(p[5], out int ex) ? ex : 0,
                    Confidence = int.TryParse(p[6], out int cf) ? cf : 2,
                    Phylesis = int.TryParse(p[7], out int ph) ? ph : 0
                })
                .ToList();

        public Tree buildTree(string racine, List<TreeNode> l){
            treeNodeLookup.Clear(); 
            var root = buildTreeRecursive(racine, l);
            return root;
        }

        private Tree buildTreeRecursive(string nodeName, List<TreeNode> l){
            var t = new Tree { Node = nodeName };
            treeNodeLookup[nodeName] = t; 

            foreach (var child in l.Where(n => n.Parent == nodeName).Select(n => n.Child)){
                t.Children.Add(buildTreeRecursive(child, l));
            }

            return t;
        }

        private void calculTaillSousArbre(Tree node){
            node.SubtreeSize = 1 + node.Children.Sum(c => {
                calculTaillSousArbre(c);
                return c.SubtreeSize;
            });
        }

        private void Form1_Paint(object sender, PaintEventArgs e){
            if (arbre == null){
                loadData();
                if (arbre == null) return; 
            }

            var g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            nodeHitboxes.Clear();

            int cx = (int)(ClientSize.Width / 2 + panX);
            int cy = (int)(ClientSize.Height / 2 + panY);

            drawNode(g, arbre, ClientSize.Width / 2f + panX, ClientSize.Height / 2f + panY, 0, 0, 2 * Math.PI);
        }

        void drawNode(Graphics g, Tree node, float centerX, float centerY, int depth, double angleStart, double angleSweep, float radiusStep = 1000f){
            int r = (int)(30 * zoom);
            float nodeRadius = depth * radiusStep * zoom;
            float nodeX = centerX + (float)(nodeRadius * Math.Cos(angleStart + angleSweep / 2));
            float nodeY = centerY + (float)(nodeRadius * Math.Sin(angleStart + angleSweep / 2));

            var rect = new RectangleF(nodeX - r, nodeY - r, r * 2, r * 2);

            
            if (rect.Right < 0 || rect.Bottom < 0 || rect.Left > ClientSize.Width || rect.Top > ClientSize.Height){
                if (node.Children.Count == 0) return;
            }
            else { 
                bool isSelected = selectedSubtree.Contains(node.Node);
                var brush = isSelected ? Brushes.OrangeRed : Brushes.LightBlue;
                var pen = isSelected ? new Pen(Color.Red, 2.5f) : Pens.Black;

                g.FillEllipse(brush, rect);
                g.DrawEllipse(pen, rect);
                nodeHitboxes[rect] = node.Node;

                using (var font = new Font(Font.FontFamily, Math.Max(1, Font.Size * zoom)))
                {
                    var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                    var nodeDetail = nodeDetailsList.FirstOrDefault(n => n.ID == node.Node);
                    var name = nodeDetail?.Name ?? node.Node;
                    g.DrawString(name, font, Brushes.Black, nodeX, nodeY, sf);
                }
            }

            if (node.Children.Count == 0) { 
                return;
        }

            float anglePerChild = (float)(angleSweep / node.Children.Count);

            for (int i = 0; i < node.Children.Count; i++) {
                double childAngleStart = angleStart + i * anglePerChild;
                double childAngleMid = childAngleStart + anglePerChild / 2;
                float childRadius = (depth + 1) * radiusStep * zoom;

                float childX = centerX + (float)(childRadius * Math.Cos(childAngleMid));
                float childY = centerY + (float)(childRadius * Math.Sin(childAngleMid));

                
              
                var linePen = selectedSubtree.Contains(node.Node) &&
                                selectedSubtree.Contains(node.Children[i].Node)
                            ? new Pen(Color.Red, 2.5f) : Pens.Black;

                g.DrawLine(linePen, nodeX, nodeY, childX, childY);
                
                drawNode(g, node.Children[i], centerX, centerY, depth + 1, childAngleStart, anglePerChild, radiusStep);
            }
        }
        private void Form1_MouseWheel(object s, MouseEventArgs e) {
            float old = zoom;
            zoom = e.Delta > 0 ? zoom * 1.1f : zoom / 1.1f;
            zoom = Math.Max(0.1f, Math.Min(zoom, 10f));

            float mx = e.X - ClientSize.Width / 2 - panX;
            float my = e.Y - ClientSize.Height / 2 - panY;
            panX -= mx * (zoom / old - 1);
            panY -= my * (zoom / old - 1);

            Invalidate();
        }

        private void Form1_MouseDown(object s, MouseEventArgs e) {
            if (e.Button == MouseButtons.Left) {
                isDragging = true;
                lastMousePosition = e.Location;
            }
        }

        private void Form1_MouseMove(object s, MouseEventArgs e) {
            if (isDragging) {
                panX += e.X - lastMousePosition.X;
                panY += e.Y - lastMousePosition.Y;
                lastMousePosition = e.Location;
                Invalidate();
            }
        }

        private void Form1_MouseUp(object s, MouseEventArgs e) {
            if (e.Button == MouseButtons.Left) { 
                isDragging = false;
            }
        }

        private void Form1_MouseClick(object sender, MouseEventArgs e) {
            var clicked = nodeHitboxes.FirstOrDefault(h => h.Key.Contains(e.Location));
            if (!string.IsNullOrEmpty(clicked.Value)) {
                selectedNodeId = clicked.Value;

                updateSelectedSubtree(selectedNodeId);
                showNodeInfo(selectedNodeId);

                Invalidate();
            }
        }

        private void updateSelectedSubtree(string nodeId) {
            selectedSubtree.Clear();

            if (treeNodeLookup.TryGetValue(nodeId, out Tree node)) {
                collectNodeSousArbre(node, selectedSubtree);
            }
        }

        private void collectNodeSousArbre(Tree node, HashSet<string> set) {
            set.Add(node.Node);
            foreach (var child in node.Children)
            {
                collectNodeSousArbre(child, set);
            }
        }

        private void showNodeInfo(string id) {
            var node = nodeDetailsList.FirstOrDefault(n => n.ID == id);
            if (node == null) return;

            int childCount = 0;
            bool isLeaf = true;

            if (treeNodeLookup.TryGetValue(id, out Tree treeNode)) {
                childCount = treeNode.Children.Count;
                isLeaf = childCount == 0;
            }

            string info = $"ID: {node.ID}\n" +
                          $"Name: {node.Name ?? "None"}\n" +
                          $"Description: {node.Description}\n" +
                          $"Child Nodes: {childCount}\n" +
                          $"Leaf Node: {(isLeaf ? "Yes" : "No")}\n" +
                          $"ToLWeb Link: http://tolweb.org/{node.Name}/{node.ID}\n" +
                          $"Extinct: {(node.Extinct == 1 ? "Yes" : "No")}\n" +
                          $"Confidence: {(node.Confidence == 0 ? "Confident" : node.Confidence == 1 ? "Problematic" : "Not specified")}\n" +
                          $"Phylesis: {(node.Phylesis == 0 ? "Monophyletic" : node.Phylesis == 1 ? "Uncertain monophyly" : "Not monophyletic")}";

            MessageBox.Show(info, "Node Information");
        }
    }
}