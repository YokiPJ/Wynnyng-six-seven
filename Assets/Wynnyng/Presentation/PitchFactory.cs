using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Wynnyng.Configuration;

namespace Wynnyng.Presentation
{
    public static class WorldLayers
    {
        public const int Ground = 8;
        public const int Goal = 9;
        public const int Ball = 10;
        public const int GroundMask = 1 << Ground;
    }

    public static class PitchFactory
    {
        private sealed class FlatMesh
        {
            private readonly List<Vector3> vertices = new List<Vector3>();
            private readonly List<int> indices = new List<int>();
            private readonly List<Color> colors = new List<Color>();
            public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color color)
            {
                int offset = vertices.Count;
                vertices.Add(a); vertices.Add(b); vertices.Add(c); vertices.Add(d);
                for (int i = 0; i < 4; i++) colors.Add(color);
                indices.Add(offset); indices.Add(offset + 2); indices.Add(offset + 1);
                indices.Add(offset); indices.Add(offset + 3); indices.Add(offset + 2);
            }
            public void Line(Vector3 a, Vector3 b, float width, Color color)
            {
                Vector3 side = Vector3.Cross(b - a, Vector3.up).normalized * (width * 0.5f);
                Quad(a - side, a + side, b + side, b - side, color);
            }
            public void Arc(Vector3 centre, float radius, float start, float end, int segments, float width, Color color)
            {
                Vector3 previous = centre + new Vector3(Mathf.Cos(start), 0f, Mathf.Sin(start)) * radius;
                for (int i = 1; i <= segments; i++)
                {
                    float angle = Mathf.Lerp(start, end, (float)i / segments);
                    Vector3 next = centre + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
                    Line(previous, next, width, color);
                    previous = next;
                }
            }
            public Mesh Build(RuntimeAssets assets, string name)
            {
                var mesh = assets.Own(new Mesh { name = name, indexFormat = IndexFormat.UInt32 });
                mesh.SetVertices(vertices); mesh.SetColors(colors); mesh.SetTriangles(indices, 0);
                mesh.RecalculateBounds();
                return mesh;
            }
        }

        public static Transform Build(Transform parent, RuntimeAssets assets, PitchConfig config, BallConfig ball)
        {
            var root = new GameObject("Procedural Pitch");
            root.transform.SetParent(parent, false);
            PhysicsMaterial ground = assets.Own(new PhysicsMaterial("Pitch Contact")
            {
                bounciness = 0f, bounceCombine = PhysicsMaterialCombine.Maximum,
                dynamicFriction = config.SurfaceFriction, staticFriction = config.SurfaceFriction,
                frictionCombine = PhysicsMaterialCombine.Minimum
            });
            var surface = new GameObject("Ground Collider") { layer = WorldLayers.Ground };
            surface.transform.SetParent(root.transform, false);
            surface.transform.localPosition = Vector3.down * (config.SurfaceThickness * 0.5f);
            BoxCollider collider = surface.AddComponent<BoxCollider>();
            collider.size = new Vector3(config.Width + config.Runoff * 2f, config.SurfaceThickness, config.Length + config.Runoff * 2f);
            collider.sharedMaterial = ground;
            collider.contactOffset = ball.ContactOffset;

            float halfWidth = config.Width * 0.5f;
            float halfLength = config.Length * 0.5f;
            var grass = new FlatMesh();
            grass.Quad(new Vector3(-halfWidth - config.Runoff, 0f, -halfLength - config.Runoff),
                new Vector3(halfWidth + config.Runoff, 0f, -halfLength - config.Runoff),
                new Vector3(halfWidth + config.Runoff, 0f, halfLength + config.Runoff),
                new Vector3(-halfWidth - config.Runoff, 0f, halfLength + config.Runoff), config.RunoffColor);
            for (int i = 0; i < config.GrassStripes; i++)
            {
                float start = -halfLength + config.Length * i / config.GrassStripes;
                float finish = -halfLength + config.Length * (i + 1) / config.GrassStripes;
                float elevation = config.LineElevation * 0.5f;
                grass.Quad(new Vector3(-halfWidth, elevation, start), new Vector3(halfWidth, elevation, start),
                    new Vector3(halfWidth, elevation, finish), new Vector3(-halfWidth, elevation, finish),
                    i % 2 == 0 ? config.GrassLight : config.GrassDark);
            }
            MeshObject("Grass", root.transform, grass.Build(assets, "Grass Stripes"), assets.Material(Color.white, true));
            BuildMarkings(root.transform, assets, config);
            BuildGoal(root.transform, assets, config, ball, 1f);
            BuildGoal(root.transform, assets, config, ball, -1f);
            return root.transform;
        }

        private static void BuildMarkings(Transform parent, RuntimeAssets assets, PitchConfig c)
        {
            var mesh = new FlatMesh();
            float w = c.Width * 0.5f;
            float l = c.Length * 0.5f;
            float inset = c.LineWidth * 0.5f;
            Rectangle(mesh, -w + inset, w - inset, -l + inset, l - inset, c);
            mesh.Line(Point(-w, 0f, c), Point(w, 0f, c), c.LineWidth, c.MarkingColor);
            mesh.Arc(Point(0f, 0f, c), c.CentreRadius, 0f, Mathf.PI * 2f, c.CircleSegments, c.LineWidth, c.MarkingColor);
            Spot(mesh, Point(0f, 0f, c), c);
            for (int end = -1; end <= 1; end += 2)
            {
                float goalLine = end * (l - inset);
                float penaltyEdge = end * (l - c.PenaltyAreaDepth);
                Rectangle(mesh, -c.PenaltyAreaWidth * 0.5f, c.PenaltyAreaWidth * 0.5f, goalLine, penaltyEdge, c);
                Rectangle(mesh, -c.GoalAreaWidth * 0.5f, c.GoalAreaWidth * 0.5f, goalLine, end * (l - c.GoalAreaDepth), c);
                float spotZ = end * (l - c.PenaltySpotDistance);
                Spot(mesh, Point(0f, spotZ, c), c);
                float offset = c.PenaltyAreaDepth - c.PenaltySpotDistance;
                float arcHalfAngle = Mathf.Acos(offset / c.CentreRadius);
                float arcCentre = -end * Mathf.PI * 0.5f;
                mesh.Arc(Point(0f, spotZ, c), c.CentreRadius, arcCentre - arcHalfAngle,
                    arcCentre + arcHalfAngle, c.CircleSegments, c.LineWidth, c.MarkingColor);
                for (int side = -1; side <= 1; side += 2)
                {
                    float start = side > 0 ? Mathf.PI : 0f;
                    float finish = start + side * end * Mathf.PI * 0.5f;
                    mesh.Arc(Point(side * (w - inset), end * (l - inset), c), c.CornerRadius,
                        start, finish, c.CircleSegments / 4, c.LineWidth, c.MarkingColor);
                }
            }
            MeshObject("Regulation Markings", parent, mesh.Build(assets, "Pitch Lines"), assets.Material(Color.white, true));
        }
        private static Vector3 Point(float x, float z, PitchConfig c) => new Vector3(x, c.LineElevation, z);
        private static void Rectangle(FlatMesh mesh, float left, float right, float back, float front, PitchConfig c)
        {
            mesh.Line(Point(left, back, c), Point(right, back, c), c.LineWidth, c.MarkingColor);
            mesh.Line(Point(right, back, c), Point(right, front, c), c.LineWidth, c.MarkingColor);
            mesh.Line(Point(right, front, c), Point(left, front, c), c.LineWidth, c.MarkingColor);
            mesh.Line(Point(left, front, c), Point(left, back, c), c.LineWidth, c.MarkingColor);
        }
        private static void Spot(FlatMesh mesh, Vector3 centre, PitchConfig c) =>
            mesh.Arc(centre, c.SpotRadius * 0.5f, 0f, Mathf.PI * 2f, c.CircleSegments, c.SpotRadius, c.MarkingColor);

        private static void BuildGoal(Transform parent, RuntimeAssets assets, PitchConfig c, BallConfig ball, float end)
        {
            var root = new GameObject(end > 0f ? "Goal +Z" : "Goal -Z");
            root.transform.SetParent(parent, false);
            root.transform.localPosition = new Vector3(0f, 0f, end * c.Length * 0.5f);
            root.transform.localRotation = end > 0f ? Quaternion.identity : Quaternion.Euler(0f, 180f, 0f);
            Material frame = assets.Material(c.MarkingColor);
            var framePhysics = assets.Own(new PhysicsMaterial("Goal Frame Contact")
            {
                bounciness = ball.Restitution, bounceCombine = PhysicsMaterialCombine.Maximum,
                staticFriction = c.SurfaceFriction, dynamicFriction = c.SurfaceFriction,
                frictionCombine = PhysicsMaterialCombine.Minimum
            });
            float postX = c.GoalWidth * 0.5f + c.PostRadius;
            float postHeight = c.GoalHeight + c.PostRadius;
            Post(root.transform, "Left Post", new Vector3(-postX, postHeight * 0.5f, 0f), postHeight,
                Quaternion.identity, c.PostRadius, frame, framePhysics, ball.ContactOffset);
            Post(root.transform, "Right Post", new Vector3(postX, postHeight * 0.5f, 0f), postHeight,
                Quaternion.identity, c.PostRadius, frame, framePhysics, ball.ContactOffset);
            Post(root.transform, "Crossbar", new Vector3(0f, postHeight, 0f), postX * 2f,
                Quaternion.Euler(0f, 0f, 90f), c.PostRadius, frame, framePhysics, ball.ContactOffset);

            PhysicsMaterial net = assets.Own(new PhysicsMaterial("Net Contact")
            {
                bounciness = c.NetRestitution, bounceCombine = PhysicsMaterialCombine.Minimum,
                dynamicFriction = c.NetFriction, staticFriction = c.NetFriction,
                frictionCombine = PhysicsMaterialCombine.Maximum
            });
            float half = c.GoalWidth * 0.5f;
            float thickness = c.NetColliderThickness;
            NetWall(root.transform, "Back Net Collider", new Vector3(0f, c.GoalHeight * 0.5f, c.GoalDepth),
                new Vector3(c.GoalWidth, c.GoalHeight, thickness), net, ball.ContactOffset);
            NetWall(root.transform, "Left Net Collider", new Vector3(-half, c.GoalHeight * 0.5f, c.GoalDepth * 0.5f),
                new Vector3(thickness, c.GoalHeight, c.GoalDepth), net, ball.ContactOffset);
            NetWall(root.transform, "Right Net Collider", new Vector3(half, c.GoalHeight * 0.5f, c.GoalDepth * 0.5f),
                new Vector3(thickness, c.GoalHeight, c.GoalDepth), net, ball.ContactOffset);
            NetWall(root.transform, "Roof Net Collider", new Vector3(0f, c.GoalHeight + thickness * 0.5f, c.GoalDepth * 0.5f),
                new Vector3(c.GoalWidth, thickness, c.GoalDepth), net, ball.ContactOffset);

            var vertices = new List<Vector3>();
            var indices = new List<int>();
            int horizontal = Mathf.CeilToInt(c.GoalWidth / c.NetSpacing);
            int vertical = Mathf.CeilToInt(c.GoalHeight / c.NetSpacing);
            int depth = Mathf.CeilToInt(c.GoalDepth / c.NetSpacing);
            for (int i = 0; i <= horizontal; i++)
            {
                float x = Mathf.Lerp(-half, half, (float)i / horizontal);
                NetLine(vertices, indices, new Vector3(x, 0f, c.GoalDepth), new Vector3(x, c.GoalHeight, c.GoalDepth));
                NetLine(vertices, indices, new Vector3(x, c.GoalHeight, 0f), new Vector3(x, c.GoalHeight, c.GoalDepth));
            }
            for (int i = 0; i <= vertical; i++)
            {
                float y = c.GoalHeight * i / vertical;
                NetLine(vertices, indices, new Vector3(-half, y, c.GoalDepth), new Vector3(half, y, c.GoalDepth));
                NetLine(vertices, indices, new Vector3(-half, y, 0f), new Vector3(-half, y, c.GoalDepth));
                NetLine(vertices, indices, new Vector3(half, y, 0f), new Vector3(half, y, c.GoalDepth));
            }
            for (int i = 0; i <= depth; i++)
            {
                float z = c.GoalDepth * i / depth;
                NetLine(vertices, indices, new Vector3(-half, 0f, z), new Vector3(-half, c.GoalHeight, z));
                NetLine(vertices, indices, new Vector3(half, 0f, z), new Vector3(half, c.GoalHeight, z));
                NetLine(vertices, indices, new Vector3(-half, c.GoalHeight, z), new Vector3(half, c.GoalHeight, z));
            }
            var mesh = assets.Own(new Mesh { name = "Net Grid" });
            mesh.SetVertices(vertices); mesh.SetIndices(indices, MeshTopology.Lines, 0); mesh.RecalculateBounds();
            MeshObject("Procedural Net", root.transform, mesh, assets.Material(c.NetColor));
        }
        private static void Post(Transform root, string name, Vector3 position, float height, Quaternion rotation,
            float radius, Material material, PhysicsMaterial physics, float contactOffset)
        {
            var frame = new GameObject(name) { layer = WorldLayers.Goal };
            frame.transform.SetParent(root, false);
            frame.transform.SetLocalPositionAndRotation(position, rotation);
            CapsuleCollider collider = frame.AddComponent<CapsuleCollider>();
            collider.radius = radius; collider.height = height + radius * 2f;
            collider.sharedMaterial = physics; collider.contactOffset = contactOffset;
            GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            visual.name = "Frame Visual";
            Collider primitiveCollider = visual.GetComponent<Collider>();
            primitiveCollider.enabled = false; Object.Destroy(primitiveCollider);
            visual.transform.SetParent(frame.transform, false);
            visual.transform.localScale = new Vector3(radius * 2f, height * 0.5f, radius * 2f);
            visual.GetComponent<MeshRenderer>().sharedMaterial = material;
        }
        private static void NetWall(Transform root, string name, Vector3 position, Vector3 size, PhysicsMaterial material, float contactOffset)
        {
            var wall = new GameObject(name) { layer = WorldLayers.Goal };
            wall.transform.SetParent(root, false); wall.transform.localPosition = position;
            BoxCollider collider = wall.AddComponent<BoxCollider>();
            collider.size = size; collider.sharedMaterial = material; collider.contactOffset = contactOffset;
        }
        private static void NetLine(List<Vector3> vertices, List<int> indices, Vector3 a, Vector3 b)
        {
            indices.Add(vertices.Count); vertices.Add(a);
            indices.Add(vertices.Count); vertices.Add(b);
        }
        private static GameObject MeshObject(string name, Transform parent, Mesh mesh, Material material)
        {
            var obj = new GameObject(name);
            obj.transform.SetParent(parent, false);
            obj.AddComponent<MeshFilter>().sharedMesh = mesh;
            obj.AddComponent<MeshRenderer>().sharedMaterial = material;
            return obj;
        }
    }
}
