// Procedural Pixel Creatures - leg shape block and the shared leg solver.
//
// Legs are built in their standing rest pose. The same solver is used by the motion system at
// runtime, so the rest pose is exactly the pose the gait starts from.

using System;
using PixelCreatures.Core.Anatomy;

namespace PixelCreatures.Core.Shapes
{
    public sealed class LegSpec
    {
        public string Name = "leg";
        public int Side;
        public int Pair;
        /// <summary>Front legs bend the elbow backwards; hind legs bend the knee forwards.</summary>
        public bool Front;
        public int AnchorBone;
        public Vec3 Hip;
        /// <summary>Rest contact point on the ground (y = 0).</summary>
        public Vec3 Contact;
        public LegStyle Style = LegStyle.Digitigrade;
        public double Length = 16;
        /// <summary>Segment fractions: upper, lower, distal (meta or foot).</summary>
        public double Upper = 0.38, Lower = 0.36, Distal = 0.26;
        public double RadiusTop = 3, RadiusKnee = 2, RadiusAnkle = 1.5, RadiusFoot = 1.6;
        /// <summary>Haunch/shoulder muscle size relative to RadiusTop (0 = none).</summary>
        public double Haunch = 1.0;
        public double PawLength = 3;
        public double PawRadius = 1.6;
        public MaterialSlot Material = MaterialSlot.Primary;
        public MaterialSlot FootMaterial = MaterialSlot.Primary;
        public int Claws;
        public double ClawSize = 1;
        public double Phase;
        public double LiftHeight = 3;
        /// <summary>Angle of the distal segment from vertical in degrees (digitigrade hock, carpus).</summary>
        public double MetaAngle = 25;
        public double BlendRadius = 1.5;
        public double DomainLength = 16;
        public bool IsArm;
        /// <summary>Group index to use; -1 creates a new group.</summary>
        public int Group = -1;
        /// <summary>Hands/paws: draw fingers instead of a paw (humanoids).</summary>
        public bool Hand;
        /// <summary>Optional elbow/knee pole hint (forward, up, outward weights).</summary>
        public Vec3 Pole;
        /// <summary>Skip the paw/foot/hoof shape (the caller attaches its own end piece, e.g. a pincer).</summary>
        public bool NoFoot;
        /// <summary>Unguligrade: split (cloven) hoof of cattle, deer and pigs instead of a solid hoof.</summary>
        public bool Cloven;
        /// <summary>Number of toes drawn at high pixel density (0 = automatic from the style).</summary>
        public int Toes;
        /// <summary>Hair tuft around the fetlock/ankle at high density (0..1, heavy horses, yetis, griffins).</summary>
        public double Feathering;
        /// <summary>Muscle definition at high density (0 = none, 1 = athletic).</summary>
        public double Muscle = 0.7;
        /// <summary>
        /// Bird leg at high density: a bare tarsus in the foot material ringed with scutes, three forward toes
        /// and a hind toe (hallux) with talons instead of a mammal paw. Feathering then covers the tarsus from
        /// the ankle down (0 = bare, 0.5 = raptor trousers, 1 = feathered to the toes like an owl).
        /// </summary>
        public bool Avian;
    }

    public static class Limbs
    {
        /// <summary>
        /// Solves joint positions of a leg in creature space.
        /// joints[0] hip, [1] knee/elbow, [2] ankle/wrist, [3] foot tip (contact end).
        /// </summary>
        public static void Solve(LegRig leg, Vec3 hip, Vec3 contact, Vec3 forward, Vec3 outward, double toeRoll, Vec3[] joints)
        {
            Vec3 up = Vec3.UnitY;
            double l1 = leg.SegmentLengths[0], l2 = leg.SegmentLengths[1], l3 = leg.SegmentLengths[2];
            double kneeSign = leg.BendSigns.Length > 0 ? leg.BendSigns[0] : 1;
            joints[0] = hip;
            if (leg.IsArm)
            {
                // shoulder -> elbow -> wrist reaching the target; the hand continues the forearm
                Vec3 armPole = leg.Pole.LengthSquared > 1e-9
                    ? forward * leg.Pole.X + up * leg.Pole.Y + outward * leg.Pole.Z
                    : forward * (kneeSign * 0.8) + outward * 0.45 - up * 0.2;
                Vec3 elbow = Kinematics.TwoBone(hip, contact, l1, l2, armPole, out Vec3 wrist);
                Vec3 fore = (wrist - elbow).NormalizedOr(up * -1);
                Vec3 handDir = (fore * 0.85 - up * 0.3 + forward * (0.25 + toeRoll)).NormalizedOr(fore);
                joints[1] = elbow;
                joints[2] = wrist;
                joints[3] = wrist + handDir * l3;
                return;
            }
            switch (leg.Style)
            {
                case LegStyle.Plantigrade:
                case LegStyle.Stump:
                {
                    // heel above the rear of the contact patch, toes forward
                    Vec3 ankle = contact - forward * (l3 * 0.35) + up * leg.SoleHeight;
                    Vec3 pole = forward * kneeSign + outward * 0.15;
                    Vec3 knee = Kinematics.TwoBone(hip, ankle, l1, l2, pole, out Vec3 ankleReached);
                    Vec3 toeDir = (forward * DMath.Cos(toeRoll) - up * DMath.Sin(toeRoll)).Normalized();
                    joints[1] = knee;
                    joints[2] = ankleReached;
                    joints[3] = ankleReached + toeDir * l3 - up * leg.SoleHeight * 0.5;
                    break;
                }
                case LegStyle.Sprawling:
                {
                    Vec3 ankle = contact + up * leg.SoleHeight;
                    Vec3 pole = outward * 1.0 + up * 0.8 + forward * kneeSign * 0.25;
                    Vec3 knee = Kinematics.TwoBone(hip, ankle, l1, l2, pole, out Vec3 ankleReached);
                    joints[1] = knee;
                    joints[2] = ankleReached;
                    joints[3] = ankleReached + (forward * 0.6 + outward * 0.8).Normalized() * l3 - up * leg.SoleHeight * 0.6;
                    break;
                }
                case LegStyle.Arthropod:
                {
                    Vec3 foot = contact + up * leg.SoleHeight;
                    Vec3 pole = up * 1.2 + outward * 0.45;
                    // distal segment points steeply down to the foot
                    Vec3 metaDir = (up * 0.92 + outward * 0.35).Normalized();
                    Kinematics.ThreeBone(hip, foot, l1, l2, l3, metaDir, pole, out Vec3 knee, out Vec3 ankle, out Vec3 footOut);
                    joints[1] = knee;
                    joints[2] = ankle;
                    joints[3] = footOut;
                    break;
                }
                default:
                {
                    // digitigrade / unguligrade: hock (hind) or carpus (front) behind and above the paw
                    Vec3 foot = contact + up * leg.SoleHeight;
                    double a = leg.MetaAngle * DMath.Deg2Rad;
                    Vec3 metaDir = (up * DMath.Cos(a) - forward * DMath.Sin(a)).Normalized();
                    Vec3 pole = forward * kneeSign + outward * 0.12;
                    Kinematics.ThreeBone(hip, foot, l1, l2, l3, metaDir, pole, out Vec3 knee, out Vec3 ankle, out Vec3 footOut);
                    joints[1] = knee;
                    joints[2] = ankle;
                    joints[3] = footOut;
                    break;
                }
            }
        }

        /// <summary>Builds bones, primitives and the rig entry of one leg in its rest pose.</summary>
        public static LegRig Build(AnatomyBuilder b, LegSpec s)
        {
            int group = s.Group >= 0 ? s.Group : b.Group(s.Name, s.BlendRadius, s.Side);
            double l1 = s.Length * s.Upper, l2 = s.Length * s.Lower, l3 = s.Length * s.Distal;
            var leg = new LegRig
            {
                Name = s.Name,
                Side = s.Side,
                Pair = s.Pair,
                AnchorBone = s.AnchorBone,
                SegmentLengths = new[] { l1, l2, l3 },
                TotalLength = l1 + l2 + l3,
                Style = s.Style,
                Phase = s.Phase,
                LiftHeight = s.LiftHeight,
                IsArm = s.IsArm,
                FootRadius = s.PawRadius,
                SoleHeight = s.Style == LegStyle.Plantigrade || s.Style == LegStyle.Stump ? Math.Max(0.6, s.PawRadius * 0.55) : Math.Max(0.6, s.PawRadius * 0.7),
                BendSigns = new[] { s.Front ? -1.0 : 1.0 },
                MetaAngle = s.MetaAngle,
                Group = group,
                Pole = s.Pole,
            };
            Vec3 outward = new Vec3(0, 0, s.Side == 0 ? 1 : s.Side);
            var joints = new Vec3[4];
            Solve(leg, s.Hip, s.Contact, Vec3.UnitX, outward, 0, joints);
            leg.RestFoot = s.Contact;

            Vec3 up = Vec3.UnitX; // bone Y axis leans to the creature front -> theta 0 = front of the leg
            int hip = b.BoneAlong(s.Name + ".hip", s.AnchorBone, joints[0], joints[1] - joints[0], up, BoneRole.LegUpper, s.Side, l1);
            int knee = b.BoneAlong(s.Name + ".knee", hip, joints[1], joints[2] - joints[1], up, BoneRole.LegLower, s.Side, l2);
            int ankle = b.BoneAlong(s.Name + ".ankle", knee, joints[2], joints[3] - joints[2], up, BoneRole.LegMeta, s.Side, l3);
            // feet point forwards; hands (and pincers) point along the hand segment
            int foot = s.IsArm
                ? b.BoneAlong(s.Name + ".foot", ankle, joints[3], joints[3] - joints[2], up, BoneRole.Hand, s.Side)
                : b.BoneAlong(s.Name + ".foot", ankle, joints[3], Vec3.UnitX, Vec3.UnitY, BoneRole.Foot, s.Side);
            leg.RootBone = hip;
            leg.JointBones = new[] { knee, ankle, foot };
            leg.ToeBones = new[] { foot };

            double len = s.DomainLength > 0 ? s.DomainLength : leg.TotalLength;
            double u1 = l1 / leg.TotalLength, u2 = (l1 + l2) / leg.TotalLength;
            var mat = s.Material;
            // haunch / shoulder muscle gives near legs a readable contour against the body
            if (s.Haunch > 0.05)
            {
                double hl = l1 * 0.62;
                var h = b.Ellipsoid(group, hip, new Vec3(l1 * 0.26, 0, 0), new Vec3(hl * 0.6, s.RadiusTop * 1.18 * s.Haunch, s.RadiusTop * 0.95 * s.Haunch),
                    mat, PatternDomain.Limb, 0, u1 * 0.6, null, s.Side);
                h.DomainLength = len;
                h.Tag = s.Name + ".haunch";
            }
            var p1 = b.Segment(group, hip, s.RadiusTop, knee, s.RadiusKnee, mat, PatternDomain.Limb, 0, u1, s.Side);
            p1.DomainLength = len; p1.Tag = s.Name + ".upper";
            var p2 = b.Segment(group, knee, s.RadiusKnee, ankle, s.RadiusAnkle, mat, PatternDomain.Limb, u1, u2, s.Side);
            p2.DomainLength = len; p2.Tag = s.Name + ".lower";
            if (s.NoFoot)
            {
                var p3 = b.Segment(group, ankle, s.RadiusAnkle, foot, Math.Max(0.45, s.RadiusFoot), mat, PatternDomain.Limb, u2, 1, s.Side);
                p3.DomainLength = len; p3.Tag = s.Name + ".distal";
            }
            else if (s.Style == LegStyle.Plantigrade || s.Style == LegStyle.Stump)
            {
                if (s.Hand)
                {
                    BuildHand(b, group, ankle, foot, s, len);
                }
                else
                {
                    // flat foot: ellipsoid from heel to toes
                    var f = b.Ellipsoid(group, foot, new Vec3(-l3 * 0.42, s.PawRadius * 0.1, 0), new Vec3(l3 * 0.62, s.PawRadius * 0.62, s.PawRadius * 0.95),
                        s.FootMaterial, PatternDomain.Limb, 0.9, 1.0, null, s.Side);
                    f.DomainLength = len; f.Tag = s.Name + ".foot";
                    var c = b.Segment(group, ankle, s.RadiusAnkle, foot, s.PawRadius * 0.7, s.FootMaterial, PatternDomain.Limb, u2, 1, s.Side);
                    c.DomainLength = len;
                }
            }
            else if (s.Style == LegStyle.Arthropod)
            {
                var p3 = b.Segment(group, ankle, s.RadiusAnkle, foot, Math.Max(0.45, s.RadiusFoot * 0.6), mat, PatternDomain.Limb, u2, 1, s.Side);
                p3.DomainLength = len; p3.Tag = s.Name + ".distal";
            }
            else
            {
                var p3 = b.Segment(group, ankle, s.RadiusAnkle, foot, s.RadiusFoot, mat, PatternDomain.Limb, u2, 0.95, s.Side);
                p3.DomainLength = len; p3.Tag = s.Name + ".meta";
                if (s.Style == LegStyle.Unguligrade)
                {
                    var hoof = b.Cone(group, foot, new Vec3(0.2, s.PawRadius * 0.3, 0), s.PawRadius * 0.95, foot, new Vec3(s.PawLength * 0.35, s.PawRadius * 0.15, 0), s.PawRadius * 0.85,
                        MaterialSlot.Accent, PatternDomain.None, 0, 1, s.Side);
                    hoof.ShadeBias = -1;
                    hoof.Tag = s.Name + ".hoof";
                    hoof.Flags |= PrimFlags.NoPattern;
                }
                else
                {
                    var paw = b.Ellipsoid(group, foot, new Vec3(s.PawLength * 0.28, s.PawRadius * 0.12, 0),
                        new Vec3(s.PawLength * 0.55, s.PawRadius * 0.7, s.PawRadius * 0.95), s.FootMaterial, PatternDomain.Limb, 0.95, 1.0, null, s.Side);
                    paw.DomainLength = len; paw.Tag = s.Name + ".paw";
                    // bird feet: the classic paw blob gives way to separate toes at high density
                    if (s.Avian) paw.MaxDensity = HdDensity;
                }
            }
            if (!s.NoFoot) BuildDetail(b, group, hip, knee, ankle, foot, s, l1, l2, l3, len, u1, u2);
            if (s.Claws > 0 && s.ClawSize > 0.2 && !s.Hand && !s.NoFoot)
            {
                for (int i = 0; i < s.Claws; i++)
                {
                    double zo = s.Claws == 1 ? 0 : (i / (double)(s.Claws - 1) - 0.5) * s.PawRadius * 1.1;
                    Vec3 a = new Vec3(s.PawLength * 0.62, s.PawRadius * 0.25, zo);
                    Vec3 tip = a + new Vec3(s.ClawSize * 1.1, -s.PawRadius * 0.35 - 0.4, zo * 0.2);
                    var claw = b.Cone(group, foot, a, Math.Max(0.45, s.ClawSize * 0.45), foot, tip, 0.3, MaterialSlot.Accent, PatternDomain.None, 0, 1, s.Side);
                    claw.Flags |= PrimFlags.Thin | PrimFlags.NoPattern | PrimFlags.Hard;
                    claw.MinQuality = 1;
                    claw.Tag = s.Name + ".claw";
                    if (HasToes(s)) claw.MaxDensity = HdDensity;
                }
            }
            return leg;
        }

        /// <summary>Pixel density from which the high density anatomy details are drawn.</summary>
        public const double HdDensity = 1.5;

        private static bool HasToes(LegSpec s) =>
            !s.Hand && !s.IsArm && (s.Style == LegStyle.Digitigrade || s.Style == LegStyle.Plantigrade || s.Style == LegStyle.Sprawling);

        private static PrimitiveDef Hd(PrimitiveDef p, string tag)
        {
            p.MinDensity = HdDensity;
            p.Tag = tag;
            return p;
        }

        /// <summary>
        /// High density anatomy: muscle bellies on the upper and lower segments, joint knobs, separate toes
        /// with claws, cloven or solid hooves with a coronet band and fetlock, and optional feathering. All
        /// parts are drawn only at a pixel density of at least HdDensity, so classic sprites are unchanged.
        /// </summary>
        private static void BuildDetail(AnatomyBuilder b, int group, int hip, int knee, int ankle, int foot, LegSpec s,
            double l1, double l2, double l3, double len, double u1, double u2)
        {
            if (s.Avian && !s.IsArm && !s.Hand && s.Style != LegStyle.Arthropod)
            {
                BuildAvian(b, group, ankle, foot, s, l3, len);
                return;
            }
            var mat = s.Material;
            bool arthropod = s.Style == LegStyle.Arthropod;
            double m = DMath.Saturate(s.Muscle);
            // bone frames: +X along the segment, +Y towards the creature front (theta 0 = front of the leg)
            if (!arthropod && m > 0.05 && s.RadiusTop > 1.0)
            {
                // upper segment: a tapering muscle belly (thigh / upper arm), fuller at the back of hind legs
                double back = s.Front ? 0.1 : -0.22;
                var thigh = b.Ellipsoid(group, hip, new Vec3(l1 * 0.36, s.RadiusTop * back, 0),
                    new Vec3(l1 * 0.42, s.RadiusTop * DMath.Lerp(0.95, 1.12, m), s.RadiusTop * DMath.Lerp(0.85, 0.98, m)), mat, PatternDomain.Limb, 0, u1, null, s.Side);
                thigh.DomainLength = len;
                Hd(thigh, s.Name + ".thigh");
                // lower segment: calf (hind, at the back) or forearm muscle (front, at the front), tapering to the joint
                double side = s.IsArm || s.Front ? 0.35 : -0.42;
                double rk = s.RadiusKnee;
                var calf = b.Ellipsoid(group, knee, new Vec3(l2 * 0.3, rk * side, 0),
                    new Vec3(l2 * 0.32, rk * DMath.Lerp(0.95, 1.18, m), rk * DMath.Lerp(0.85, 1.0, m)), mat, PatternDomain.Limb, u1, u1 + (u2 - u1) * 0.5, null, s.Side);
                calf.DomainLength = len;
                Hd(calf, s.Name + ".calf");
                // joint knobs: knee cap / elbow point and the hock or wrist
                double kn = rk * 0.62;
                var knob = b.Ellipsoid(group, knee, new Vec3(0, rk * (s.Front || s.IsArm ? -0.35 : 0.4), 0), new Vec3(kn, kn, kn * 0.9), mat, PatternDomain.Limb, u1 - 0.02, u1 + 0.02, null, s.Side);
                knob.DomainLength = len;
                Hd(knob, s.Name + ".kneecap");
                if (s.Style == LegStyle.Digitigrade || s.Style == LegStyle.Unguligrade)
                {
                    double an = s.RadiusAnkle * 0.85;
                    var hock = b.Ellipsoid(group, ankle, new Vec3(an * 0.2, s.Front ? an * 0.3 : -an * 0.55, 0), new Vec3(an, an * 0.9, an * 0.8), mat, PatternDomain.Limb, u2 - 0.02, u2 + 0.02, null, s.Side);
                    hock.DomainLength = len;
                    Hd(hock, s.Name + ".hock");
                }
            }
            if (arthropod)
            {
                // chitin joints: darker, slightly wider rings at the knee and ankle; bristles on the lower segments
                foreach (var (bone, r) in new[] { (knee, s.RadiusKnee), (ankle, s.RadiusAnkle) })
                {
                    var ring = b.Ellipsoid(group, bone, new Vec3(0.15, 0, 0), new Vec3(Math.Max(0.45, r * 0.45), r * 1.18, r * 1.18), mat, PatternDomain.Limb, 0, 1, null, s.Side);
                    ring.Flags |= PrimFlags.NoPattern | PrimFlags.Hard;
                    ring.ShadeBias = -1;
                    Hd(ring, s.Name + ".joint");
                }
                int bristles = 3;
                for (int k = 0; k < bristles; k++)
                {
                    double t = (k + 0.6) / (bristles + 0.4);
                    Vec3 at = new Vec3(l2 * t, 0, 0);
                    double r = DMath.Lerp(s.RadiusKnee, s.RadiusAnkle, t);
                    Vec3 tip = at + new Vec3(r * 0.4, -r * 1.6 - 0.6, (k % 2 == 0 ? 1 : -1) * r * 0.6);
                    var hair = b.Cone(group, knee, at + new Vec3(0, -r * 0.6, 0), 0.32, knee, tip, 0.25, MaterialSlot.Marking, PatternDomain.None, 0, 1, s.Side);
                    hair.Flags |= PrimFlags.Thin | PrimFlags.NoPattern | PrimFlags.Hard | PrimFlags.NoShadow;
                    Hd(hair, s.Name + ".bristle");
                }
                for (int k = 0; k < 2; k++)
                {
                    double t = (k + 1) / 3.0;
                    double r = DMath.Lerp(s.RadiusAnkle, Math.Max(0.45, s.RadiusFoot * 0.6), t);
                    Vec3 at = new Vec3(l3 * t, -r * 0.5, 0);
                    var spine = b.Cone(group, ankle, at, 0.3, ankle, at + new Vec3(r * 0.2 + 0.4, -r * 1.2 - 0.5, 0), 0.22, MaterialSlot.Marking, PatternDomain.None, 0, 1, s.Side);
                    spine.Flags |= PrimFlags.Thin | PrimFlags.NoPattern | PrimFlags.Hard | PrimFlags.NoShadow;
                    Hd(spine, s.Name + ".spine");
                }
                return;
            }
            if (s.IsArm || s.Hand) return;

            if (s.Style == LegStyle.Unguligrade)
            {
                double pr = s.PawRadius;
                // pastern/fetlock: the joint above the hoof bulges at the back
                var fet = b.Ellipsoid(group, foot, new Vec3(-pr * 0.25, pr * 1.15, 0), new Vec3(pr * 0.85, pr * 0.8, pr * 0.78), mat, PatternDomain.Limb, 0.93, 0.96, null, s.Side);
                fet.DomainLength = len;
                Hd(fet, s.Name + ".fetlock");
                // coronet band: a slightly wider, darker rim at the top of the hoof
                var cor = b.Cone(group, foot, new Vec3(0.1, pr * 0.62, 0), pr * 1.0, foot, new Vec3(pr * 0.35, pr * 0.58, 0), pr * 0.95, MaterialSlot.Accent, PatternDomain.None, 0, 1, s.Side);
                cor.ShadeBias = -2;
                cor.Flags |= PrimFlags.NoPattern | PrimFlags.Hard;
                Hd(cor, s.Name + ".coronet");
                if (s.Cloven)
                {
                    // two toes with a dark cleft between them; dew claws at the back
                    for (int t = -1; t <= 1; t += 2)
                    {
                        double z = t * pr * 0.4;
                        var half = b.Cone(group, foot, new Vec3(0.1, pr * 0.32, z), pr * 0.55, foot, new Vec3(s.PawLength * 0.4, pr * 0.12, z * 1.08), pr * 0.45,
                            MaterialSlot.Accent, PatternDomain.None, 0, 1, s.Side);
                        half.ShadeBias = -1;
                        half.Flags |= PrimFlags.NoPattern | PrimFlags.Hard;
                        Hd(half, s.Name + ".hoofhalf");
                        var dew = b.Ellipsoid(group, foot, new Vec3(-pr * 0.75, pr * 0.85, z * 0.9), new Vec3(pr * 0.3, pr * 0.22, pr * 0.2), MaterialSlot.Accent, PatternDomain.None, 0, 1, null, s.Side);
                        dew.ShadeBias = -1;
                        dew.Flags |= PrimFlags.NoPattern | PrimFlags.Hard;
                        Hd(dew, s.Name + ".dewclaw");
                    }
                }
                else
                {
                    // solid hoof: a lighter, slightly flared wall with a dark sole line
                    var wall = b.Cone(group, foot, new Vec3(0.15, pr * 0.42, 0), pr * 0.9, foot, new Vec3(s.PawLength * 0.3, pr * 0.18, 0), pr * 1.0,
                        MaterialSlot.Accent, PatternDomain.None, 0, 1, s.Side);
                    wall.Flags |= PrimFlags.NoPattern | PrimFlags.Hard;
                    Hd(wall, s.Name + ".hoofwall");
                }
                if (s.Feathering > 0.1) Feather(b, group, foot, s, pr, len);
                return;
            }
            if (!HasToes(s)) return;

            // toes: a row of beads along the front rim of the paw/foot, each with its own claw
            int toes = s.Toes > 0 ? s.Toes : (s.Style == LegStyle.Plantigrade ? 5 : 4);
            double pw = s.PawRadius;
            bool flat = s.Style == LegStyle.Plantigrade || s.Style == LegStyle.Stump;
            double tr = pw * (flat ? 0.34 : 0.4);
            double front = flat ? l3 * 0.2 : s.PawLength * 0.62;
            double spread = pw * (flat ? 1.05 : 0.95);
            for (int i = 0; i < toes; i++)
            {
                double t = toes == 1 ? 0 : i / (double)(toes - 1) * 2 - 1;
                double z = t * spread;
                // the middle toes reach further forward
                double x = front - Math.Abs(t) * pw * 0.45;
                var toe = b.Ellipsoid(group, foot, new Vec3(x, pw * 0.05, z), new Vec3(tr * 1.25, tr * 0.95, tr), s.FootMaterial, PatternDomain.Limb, 0.97, 1.0, null, s.Side);
                toe.DomainLength = len;
                Hd(toe, s.Name + ".toe");
                if (s.Claws > 0 && s.ClawSize > 0.2)
                {
                    Vec3 a = new Vec3(x + tr * 0.9, pw * 0.15, z);
                    Vec3 tip = a + new Vec3(s.ClawSize * 0.95, -pw * 0.3 - 0.35, z * 0.08);
                    var claw = b.Cone(group, foot, a, Math.Max(0.4, s.ClawSize * 0.32), foot, tip, 0.28, MaterialSlot.Accent, PatternDomain.None, 0, 1, s.Side);
                    claw.Flags |= PrimFlags.Thin | PrimFlags.NoPattern | PrimFlags.Hard;
                    Hd(claw, s.Name + ".toeclaw");
                }
            }
            if (!flat)
            {
                // the large central pad behind the toes keeps the paw compact from below
                var pad = b.Ellipsoid(group, foot, new Vec3(s.PawLength * 0.15, -pw * 0.05, 0), new Vec3(pw * 0.7, pw * 0.5, pw * 0.75), s.FootMaterial, PatternDomain.Limb, 0.96, 1.0, null, s.Side);
                pad.DomainLength = len;
                Hd(pad, s.Name + ".pad");
            }
            if (s.Feathering > 0.1) Feather(b, group, foot, s, pw, len);
        }

        /// <summary>Long hair around the ankle that falls over the foot (draft horses, yetis, owls' legs).</summary>
        private static void Feather(AnatomyBuilder b, int group, int foot, LegSpec s, double pr, double len)
        {
            double f = DMath.Saturate(s.Feathering);
            for (int k = -1; k <= 1; k++)
            {
                Vec3 a = new Vec3(-pr * 0.2, pr * (1.6 + f), k * pr * 0.55);
                Vec3 tip = new Vec3(-pr * (0.6 + 0.4 * f), pr * 0.2, k * pr * 0.85);
                var tuft = b.Cone(group, foot, a, pr * (0.55 + 0.25 * f), foot, tip, pr * 0.25, MaterialSlot.Hair, PatternDomain.Limb, 0.9, 0.97, s.Side);
                tuft.DomainLength = len;
                Hd(tuft, s.Name + ".feather");
            }
        }

        /// <summary>
        /// High density bird leg: the tarsus is bare skin in the foot material, ringed with scutes (or feathered
        /// from the ankle down), and the foot splits into three forward toes and a hind toe, each with a talon.
        /// </summary>
        private static void BuildAvian(AnatomyBuilder b, int group, int ankle, int foot, LegSpec s, double l3, double len)
        {
            double ra = s.RadiusAnkle, rf = Math.Max(0.45, s.RadiusFoot);
            double feathered = DMath.Saturate(s.Feathering);
            // ankle bone frame: +X runs down the tarsus to the foot
            var tarsus = b.Cone(group, ankle, Vec3.Zero, ra * 1.06, ankle, new Vec3(l3, 0, 0), rf * 1.06, s.FootMaterial, PatternDomain.Limb, 0.8, 0.95, s.Side);
            tarsus.DomainLength = len;
            Hd(tarsus, s.Name + ".tarsus");
            if (feathered > 0.05)
            {
                // trousers: a feathered sleeve from the ankle down, flaring a little at its lower rim
                double end = l3 * DMath.Lerp(0.35, 1.0, feathered);
                var sleeve = b.Cone(group, ankle, new Vec3(-ra * 0.2, 0, 0), ra * 1.3, ankle, new Vec3(end, 0, 0), DMath.Lerp(ra, rf, end / l3) * 1.4, s.Material, PatternDomain.Limb, 0.75, 0.9, s.Side);
                sleeve.DomainLength = len;
                Hd(sleeve, s.Name + ".trousers");
            }
            // scutes: thin, slightly darker rings down the bare part of the tarsus
            double spacing = Math.Max(1.1, ra * 0.95);
            double bare0 = feathered > 0.05 ? l3 * DMath.Lerp(0.35, 1.0, feathered) + spacing * 0.5 : spacing * 0.6;
            for (double x = bare0; x < l3 - spacing * 0.35; x += spacing)
            {
                double r = DMath.Lerp(ra, rf, x / l3);
                var ring = b.Ellipsoid(group, ankle, new Vec3(x, 0, 0), new Vec3(Math.Max(0.38, spacing * 0.22), r * 1.2, r * 1.2), s.FootMaterial, PatternDomain.None, 0, 1, null, s.Side);
                ring.Flags |= PrimFlags.NoPattern | PrimFlags.Hard;
                ring.ShadeBias = -1;
                Hd(ring, s.Name + ".scute");
            }
            // anisodactyl foot (foot bone frame: +X creature forward, +Y up): three toes fan forward, the
            // hallux points back; all of them rest on the ground and end in a talon
            double pr = s.PawRadius, pl = s.PawLength;
            double sole = Math.Max(0.6, pr * 0.7);
            double r0 = Math.Max(0.35, pr * 0.42), r1 = Math.Max(0.3, pr * 0.28);
            Vec3 heel = new Vec3(pr * 0.1, -sole * 0.35, 0);
            for (int i = -1; i <= 2; i++)
            {
                bool hallux = i == 2;
                double a = hallux ? DMath.Pi : i * 0.62;
                double tl = hallux ? pl * 0.55 : pl * (i == 0 ? 1.05 : 0.82);
                Vec3 tip = new Vec3(DMath.Cos(a) * tl, -sole + r1 + 0.05, DMath.Sin(a) * tl);
                var toe = b.Cone(group, foot, heel, r0, foot, tip, r1, s.FootMaterial, PatternDomain.Limb, 0.97, 1.0, s.Side);
                toe.DomainLength = len;
                Hd(toe, s.Name + (hallux ? ".hallux" : ".toe"));
                if (s.Claws > 0 && s.ClawSize > 0.2)
                {
                    Vec3 dir = new Vec3(DMath.Cos(a), 0, DMath.Sin(a));
                    Vec3 talon = tip + dir * (s.ClawSize * 0.75) + new Vec3(0, -Math.Max(0.3, s.ClawSize * 0.45), 0);
                    var claw = b.Cone(group, foot, tip - dir * (r1 * 0.3), Math.Max(0.32, s.ClawSize * 0.32), foot, talon, 0.26, MaterialSlot.Accent, PatternDomain.None, 0, 1, s.Side);
                    claw.Flags |= PrimFlags.Thin | PrimFlags.NoPattern | PrimFlags.Hard;
                    claw.ShadeBias = -2;
                    Hd(claw, s.Name + ".talon");
                }
            }
        }

        private static void BuildHand(AnatomyBuilder b, int group, int wrist, int hand, LegSpec s, double len)
        {
            // mitt shaped hand with a thumb; fingers are implied by the silhouette at pixel scale
            var palm = b.Ellipsoid(group, hand, new Vec3(-s.PawLength * 0.25, 0, 0), new Vec3(s.PawLength * 0.55, s.PawRadius * 0.9, s.PawRadius * 0.75),
                s.FootMaterial, PatternDomain.Limb, 0.92, 1.0, null, s.Side);
            palm.DomainLength = len; palm.Tag = s.Name + ".palm";
            var wristC = b.Segment(group, wrist, s.RadiusAnkle, hand, s.PawRadius * 0.75, s.FootMaterial, PatternDomain.Limb, 0.8, 0.95, s.Side);
            wristC.DomainLength = len;
            // high density: four curled fingers and a thumb instead of a mitt
            double pr = s.PawRadius, pl = s.PawLength;
            double fr = Math.Max(0.4, pr * 0.3);
            for (int i = 0; i < 4; i++)
            {
                double z = (i / 3.0 - 0.5) * pr * 1.35;
                double reach = pl * (0.55 + 0.12 * (1 - Math.Abs(i / 3.0 - 0.5) * 2));
                Vec3 k0 = new Vec3(pl * 0.12, -pr * 0.05, z);
                Vec3 k1 = new Vec3(pl * 0.12 + reach * 0.6, -pr * 0.25, z * 1.08);
                Vec3 k2 = k1 + new Vec3(reach * 0.25, -reach * 0.38, 0);
                var f1 = b.Cone(group, hand, k0, fr * 1.1, hand, k1, fr, s.FootMaterial, PatternDomain.Limb, 0.95, 1.0, s.Side);
                var f2 = b.Cone(group, hand, k1, fr, hand, k2, fr * 0.8, s.FootMaterial, PatternDomain.Limb, 0.97, 1.0, s.Side);
                f1.DomainLength = len; f2.DomainLength = len;
                Hd(f1, s.Name + ".finger"); Hd(f2, s.Name + ".finger");
                if (s.Claws > 0 && s.ClawSize > 0.2)
                {
                    var nail = b.Cone(group, hand, k2, Math.Max(0.35, fr * 0.6), hand, k2 + new Vec3(s.ClawSize * 0.35, -s.ClawSize * 0.55, 0), 0.25, MaterialSlot.Accent, PatternDomain.None, 0, 1, s.Side);
                    nail.Flags |= PrimFlags.Thin | PrimFlags.NoPattern | PrimFlags.Hard;
                    Hd(nail, s.Name + ".nail");
                }
            }
            double ts = s.Side == 0 ? 1 : s.Side;
            Vec3 t0 = new Vec3(-pl * 0.15, pr * 0.1, -ts * pr * 0.55);
            Vec3 t1 = t0 + new Vec3(pl * 0.35, -pr * 0.15, -ts * pr * 0.55);
            var thumb = b.Cone(group, hand, t0, fr * 1.25, hand, t1, fr * 0.85, s.FootMaterial, PatternDomain.Limb, 0.95, 1.0, s.Side);
            thumb.DomainLength = len;
            Hd(thumb, s.Name + ".thumb");
            if (s.Claws > 0 && s.ClawSize > 0.2)
            {
                for (int i = 0; i < Math.Min(3, s.Claws); i++)
                {
                    double zo = (i - 1) * s.PawRadius * 0.55;
                    var claw = b.Cone(group, hand, new Vec3(s.PawLength * 0.2, -s.PawRadius * 0.2, zo), 0.5, hand,
                        new Vec3(s.PawLength * 0.2 + s.ClawSize, -s.PawRadius * 0.7, zo * 1.1), 0.3, MaterialSlot.Accent, PatternDomain.None, 0, 1, s.Side);
                    claw.Flags |= PrimFlags.Thin | PrimFlags.NoPattern | PrimFlags.Hard;
                    claw.MinQuality = 1;
                    claw.MaxDensity = HdDensity;
                }
            }
        }
    }
}
