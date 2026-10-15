using System;
using System.Collections.Generic;
using UnityEngine;

namespace GraphSimulator
{
    public enum GraphKind { GeneralTree, BinaryTree, BinarySearchTree, UndirectedGraph, DirectedGraph, WeightedGraph }

    [Serializable]
    public sealed class GraphNode
    {
        public int id;
        public string name;
        public int key;
        public Vector2 position;
        public Color color = new Color(0.38f, 0.42f, 0.47f);
        public float size = 64f;
    }

    [Serializable]
    public sealed class GraphEdge
    {
        public int id;
        public int from;
        public int to;
        public int slot; // 0 = left, 1 = right for binary trees.
        public float weight = 1f;
        public Color color = new Color(0.49f, 0.53f, 0.57f);
        public float thickness = 3f;
    }

    [Serializable]
    public sealed class GraphDocument
    {
        public int version = 1;
        public GraphKind kind;
        public int nextNodeId = 1;
        public int nextEdgeId = 1;
        public List<GraphNode> nodes = new List<GraphNode>();
        public List<GraphEdge> edges = new List<GraphEdge>();

        public GraphNode FindNode(int id) { return nodes.Find(n => n.id == id); }
        public GraphEdge FindEdge(int id) { return edges.Find(e => e.id == id); }
        public bool IsTree { get { return kind <= GraphKind.BinarySearchTree; } }
        public bool IsDirected { get { return kind == GraphKind.DirectedGraph || IsTree; } }

        public GraphNode AddNode(Vector2 position, int key = 0)
        {
            if (kind == GraphKind.BinarySearchTree) return InsertKey(key, position);
            var node = new GraphNode { id = nextNodeId++, name = "Node " + nextNodeId, position = position };
            node.name = "Node " + node.id;
            nodes.Add(node);
            return node;
        }

        public GraphNode InsertKey(int key, Vector2 position)
        {
            if (kind != GraphKind.BinarySearchTree || nodes.Exists(n => n.key == key)) return null;
            GraphNode parent = null;
            int slot = 0;
            if (nodes.Count > 0)
            {
                var current = nodes[0];
                while (current != null)
                {
                    parent = current;
                    slot = key < current.key ? 0 : 1;
                    var edge = edges.Find(e => e.from == current.id && e.slot == slot);
                    current = edge == null ? null : FindNode(edge.to);
                }
            }
            var node = new GraphNode { id = nextNodeId++, key = key, name = key.ToString(), position = position };
            nodes.Add(node);
            if (parent != null) AddEdgeInternal(parent.id, node.id, slot);
            return node;
        }

        public bool TryAddEdge(int from, int to, out string reason)
        {
            reason = "";
            if (FindNode(from) == null || FindNode(to) == null || from == to) { reason = "Choose two different nodes."; return false; }
            if (kind == GraphKind.BinarySearchTree) { reason = "BST edges are created by key insertion."; return false; }
            if (edges.Exists(e => e.from == from && e.to == to || !IsDirected && e.from == to && e.to == from))
            { reason = "That edge already exists."; return false; }
            int slot = 0;
            if (IsTree)
            {
                if (edges.Exists(e => e.to == to)) { reason = "A tree node can have only one parent."; return false; }
                if (HasPath(to, from)) { reason = "Tree cycles are not allowed."; return false; }
                if (kind == GraphKind.BinaryTree)
                {
                    var children = edges.FindAll(e => e.from == from);
                    if (children.Count >= 2) { reason = "A binary node can have at most two children."; return false; }
                    slot = children.Exists(e => e.slot == 0) ? 1 : 0;
                }
            }
            AddEdgeInternal(from, to, slot);
            return true;
        }

        private void AddEdgeInternal(int from, int to, int slot)
        {
            edges.Add(new GraphEdge { id = nextEdgeId++, from = from, to = to, slot = slot });
        }

        private bool HasPath(int from, int target)
        {
            var seen = new HashSet<int>();
            var stack = new Stack<int>();
            stack.Push(from);
            while (stack.Count > 0)
            {
                int id = stack.Pop();
                if (id == target) return true;
                if (!seen.Add(id)) continue;
                foreach (var edge in edges) if (edge.from == id) stack.Push(edge.to);
            }
            return false;
        }

        public void RemoveNodes(ICollection<int> ids)
        {
            nodes.RemoveAll(n => ids.Contains(n.id));
            edges.RemoveAll(e => ids.Contains(e.from) || ids.Contains(e.to));
            if (kind == GraphKind.BinarySearchTree) RebuildBstEdges();
        }

        private void RebuildBstEdges()
        {
            edges.Clear();
            for (int i = 1; i < nodes.Count; i++)
            {
                var current = nodes[0];
                while (true)
                {
                    int slot = nodes[i].key < current.key ? 0 : 1;
                    var edge = edges.Find(e => e.from == current.id && e.slot == slot);
                    if (edge == null) { AddEdgeInternal(current.id, nodes[i].id, slot); break; }
                    current = FindNode(edge.to);
                }
            }
        }

        public bool Validate(out string reason)
        {
            reason = "";
            if ((int)kind < 0 || (int)kind > (int)GraphKind.WeightedGraph) { reason = "Unknown graph kind."; return false; }
            if (nodes == null || edges == null) { reason = "Missing node or edge list."; return false; }
            var nodeIds = new HashSet<int>();
            var edgeIds = new HashSet<int>();
            int maxNodeId = 0, maxEdgeId = 0;
            foreach (var node in nodes)
            {
                if (node == null || node.id <= 0 || !nodeIds.Add(node.id) || node.size < 20f || node.size > 180f)
                { reason = "Invalid node data."; return false; }
                maxNodeId = Math.Max(maxNodeId, node.id);
            }
            foreach (var edge in edges)
            {
                if (edge == null || edge.id <= 0 || !edgeIds.Add(edge.id) || !nodeIds.Contains(edge.from) || !nodeIds.Contains(edge.to) || edge.from == edge.to || edge.thickness < 1f || edge.thickness > 20f)
                { reason = "Invalid edge data."; return false; }
                maxEdgeId = Math.Max(maxEdgeId, edge.id);
            }
            if (nextNodeId <= maxNodeId || nextEdgeId <= maxEdgeId) { reason = "Invalid ID sequence."; return false; }
            if (IsTree)
            {
                var childIds = new HashSet<int>();
                foreach (var edge in edges)
                {
                    if (!childIds.Add(edge.to) || HasPath(edge.to, edge.from)) { reason = "Tree parent or cycle rule violated."; return false; }
                    if (kind != GraphKind.GeneralTree && (edge.slot < 0 || edge.slot > 1 || edges.FindAll(e => e.from == edge.from).Count > 2 || edges.FindAll(e => e.from == edge.from && e.slot == edge.slot).Count > 1))
                    { reason = "Binary child rule violated."; return false; }
                }
            }
            if (kind == GraphKind.BinarySearchTree)
            {
                var keys = new HashSet<int>();
                foreach (var node in nodes) if (!keys.Add(node.key)) { reason = "Duplicate BST key."; return false; }
                if (nodes.Count > 0 && !ValidateBst(nodes[0].id, null, null, new HashSet<int>()))
                { reason = "Invalid BST ordering."; return false; }
                if (edges.Count != Math.Max(0, nodes.Count - 1)) { reason = "Disconnected BST."; return false; }
            }
            return true;
        }

        private bool ValidateBst(int id, long? min, long? max, HashSet<int> seen)
        {
            var node = FindNode(id);
            if (node == null || !seen.Add(id) || min.HasValue && node.key <= min || max.HasValue && node.key >= max) return false;
            foreach (var edge in edges)
                if (edge.from == id && (edge.slot == 0 ? !ValidateBst(edge.to, min, node.key, seen) : !ValidateBst(edge.to, node.key, max, seen))) return false;
            return true;
        }
    }
}
