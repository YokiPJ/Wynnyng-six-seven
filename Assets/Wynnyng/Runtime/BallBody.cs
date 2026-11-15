using UnityEngine;
using Wynnyng.Configuration;
using Wynnyng.Core;
using Wynnyng.Presentation;

namespace Wynnyng.Runtime
{
    [RequireComponent(typeof(Rigidbody), typeof(SphereCollider))]
    public sealed class BallBody : MonoBehaviour, IBallBody
    {
        private Rigidbody body;
        private BallConfig config;
        private BallPhysicsParameters parameters;
        private float gravityMagnitude;
        public Rigidbody Rigidbody => body;
        public Vec3 Position => ToCore(body.position);
        public Vec3 Velocity => ToCore(body.linearVelocity);

        public void Initialize(BallConfig ball, SimulationConfig simulation, RuntimeAssets assets)
        {
            config = ball; parameters = config.Parameters;
            gravityMagnitude = simulation.Gravity.magnitude;
            gameObject.layer = WorldLayers.Ball;
            body = GetComponent<Rigidbody>();
            body.mass = config.Mass;
            body.linearDamping = 0f;
            body.angularDamping = config.AngularDamping;
            body.useGravity = true;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            body.maxAngularVelocity = config.MaxAngularSpeed;
            body.solverIterations = simulation.SolverIterations;
            body.solverVelocityIterations = simulation.SolverVelocityIterations;
            var collider = GetComponent<SphereCollider>();
            collider.radius = config.Radius;
            collider.contactOffset = config.ContactOffset;
            collider.sharedMaterial = assets.Own(new PhysicsMaterial("Ball Contact")
            {
                bounciness = config.Restitution, bounceCombine = PhysicsMaterialCombine.Average,
                dynamicFriction = config.SurfaceFriction, staticFriction = config.SurfaceFriction,
                frictionCombine = PhysicsMaterialCombine.Minimum
            });
            GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            visual.name = "Procedural Ball Visual";
            Collider primitiveCollider = visual.GetComponent<Collider>();
            primitiveCollider.enabled = false; Destroy(primitiveCollider);
            visual.transform.SetParent(transform, false);
            visual.transform.localScale = Vector3.one * (config.Radius * 2f);
            Shader shader = Resources.Load<Shader>("Shaders/Ball");
            if (shader == null) throw new System.InvalidOperationException("Missing Resources/Shaders/Ball.shader.");
            Material material = assets.Own(new Material(shader));
            material.SetColor("_BaseColor", config.BaseColor);
            material.SetColor("_PanelColor", config.PanelColor);
            material.SetFloat("_PanelFrequency", config.PanelFrequency);
            visual.GetComponent<MeshRenderer>().sharedMaterial = material;
            Teleport(ToCore(config.RestPosition));
        }

        public void FixedStep(float seconds)
        {
            Vector3 velocity = body.linearVelocity;
            bool grounded = Mathf.Abs(velocity.y) <= config.GroundedMaxVerticalSpeed &&
                Physics.Raycast(body.position, Vector3.down, config.Radius + config.GroundProbeDistance,
                    WorldLayers.GroundMask, QueryTriggerInteraction.Ignore);
            Vec3 acceleration = BallPhysics.Acceleration(ToCore(velocity), ToCore(body.angularVelocity),
                grounded, gravityMagnitude, seconds, parameters);
            body.AddForce(ToUnity(acceleration), ForceMode.Acceleration);
        }
        public void Teleport(Vec3 position)
        {
            body.position = ToUnity(position);
            body.rotation = Quaternion.identity;
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.WakeUp();
        }
        public void Launch(Vec3 velocity, Vec3 spin)
        {
            body.linearVelocity = ToUnity(velocity);
            body.angularVelocity = ToUnity(spin);
            body.WakeUp();
        }
        public static Vec3 ToCore(Vector3 value) => new Vec3(value.x, value.y, value.z);
        public static Vector3 ToUnity(Vec3 value) => new Vector3(value.X, value.Y, value.Z);
    }
}
