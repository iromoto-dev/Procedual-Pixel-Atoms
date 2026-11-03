// Procedural Pixel Creatures - hard edged solids built from triangle plates: boxes, prisms, frustums
// and pyramids for props (crates, chests, pillars, gravestones, crystals, roofs).
//
// Faces are flat plates with crisp edges (no soft union) and one shared surface mapping per face, so
// wood planks, masonry courses and metal bands line up across the two triangles of a quad. All
// coordinates are in the frame of one bone; the solid moves rigidly with it.

using System;
using System.Collections.Generic;
using PixelCreatures.Core.Anatomy;

namespace PixelCreatures.Core.Shapes
{
    public sealed class SolidStyle
    {
        public MaterialSlot Material = MaterialSlot.Primary;
        public PatternDomain Domain = PatternDomain.Body;
        /// <summary>Marks (moss, lichen, stains) from the pattern genes on this solid.</summary>
        public bool Pattern;
        /// <summary>Faceted shading: every face snaps to a chiselled normal (stone, crystal).</summary>
        public bool Faceted;
        public bool Emissive;
        public bool CastShadow = true;
        public int ShadeBias;
        public double MinDensity;
        public double MaxDensity;
        public string Tag = "solid";
        /// <summary>Rotates the surface mapping of side faces by 90 degrees (boards run up instead of around).</summary>
        public bool VerticalGrain;
    }

    public static class Solids
    {
        private static PrimitiveDef Face(AnatomyBuilder b, int group, int bone, Vec3 a, Vec3 c, Vec3 d, Vec2 ua, Vec2 uc, Vec2 ud, SolidStyle st, double domainLength)
        {
            var t = b.Triangle(group, bone, a, bone, c, bone, d, st.Material, st.Domain, 0, PrimFlags.Hard | PrimFlags.UvMapped);
            t.UvA = ua; t.UvB = uc; t.UvC = ud;
            t.DomainLength = Math.Max(1, domainLength);
            if (!st.Pattern) t.Flags |= PrimFlags.NoPattern;
            if (st.Faceted) t.Flags |= PrimFlags.Faceted;
            if (st.Emissive) t.Flags |= PrimFlags.Emissive;
            if (st.CastShadow) t.Flags |= PrimFlags.ShadowCaster;
            else t.Flags |= PrimFlags.NoShadow;
            t.ShadeBias = st.ShadeBias;
            t.MinDensity = st.MinDensity;
            t.MaxDensity = st.MaxDensity;
            t.Tag = st.Tag;
            return t;
        }

        private static void Quad(AnatomyBuilder b, int group, int bone, Vec3 p0, Vec3 p1, Vec3 p2, Vec3 p3, double w, double h, double u0, SolidStyle st, double len)
        {
            // p0 p1 along the U edge (width w), p3 above p0 (height h); boards run along U
            Vec2 a = new Vec2(u0, 0), c = new Vec2(u0 + w, 0), d = new Vec2(u0 + w, h), e = new Vec2(u0, h);
            if (st.VerticalGrain) { a = new Vec2(0, u0); c = new Vec2(0, u0 + w); d = new Vec2(h, u0 + w); e = new Vec2(h, u0); }
            Face(b, group, bone, p0, p1, p2, a, c, d, st, len);
            Face(b, group, bone, p0, p2, p3, a, d, e, st, len);
        }

        /// <summary>
        /// Axis aligned box (in the rotated frame 'rot' around 'center') with half extents 'half':
        /// X forward, Y up, Z right. Six faces, twelve triangles.
        /// </summary>
        public static void Box(AnatomyBuilder b, int group, int bone, Vec3 center, Vec3 half, SolidStyle st, Quat? rot = null)
        {
            var r = Mat3.FromQuat(rot ?? Quat.Identity);
            Vec3 P(double x, double y, double z) => center + r.Mul(new Vec3(x * half.X, y * half.Y, z * half.Z));
            double w = half.X * 2, d = half.Z * 2, h = half.Y * 2;
            double len = Math.Max(w, Math.Max(d, h));
            // sides wrap around (U runs around the box so planks are continuous), then top and bottom
            Quad(b, group, bone, P(-1, -1, 1), P(1, -1, 1), P(1, 1, 1), P(-1, 1, 1), w, h, 0, st, len);          // +Z
            Quad(b, group, bone, P(1, -1, 1), P(1, -1, -1), P(1, 1, -1), P(1, 1, 1), d, h, w, st, len);           // +X
            Quad(b, group, bone, P(1, -1, -1), P(-1, -1, -1), P(-1, 1, -1), P(1, 1, -1), w, h, w + d, st, len);   // -Z
            Quad(b, group, bone, P(-1, -1, -1), P(-1, -1, 1), P(-1, 1, 1), P(-1, 1, -1), d, h, w * 2 + d, st, len); // -X
            Quad(b, group, bone, P(-1, 1, 1), P(1, 1, 1), P(1, 1, -1), P(-1, 1, -1), w, d, 0, st, len);           // top
            Quad(b, group, bone, P(-1, -1, -1), P(1, -1, -1), P(1, -1, 1), P(-1, -1, 1), w, d, 0, st, len);       // bottom
        }

        /// <summary>
        /// Prism or frustum with a regular n-gon cross-section from 'bottom' (radius rb) to 'top' (radius rt)
        /// along 'axis' (unit, bone frame). rt = 0 makes a pyramid. 'twist' rotates the polygon (radians).
        /// Caps are closed. Returns the corner positions of the top ring.
        /// </summary>
        public static Vec3[] Prism(AnatomyBuilder b, int group, int bone, Vec3 bottom, Vec3 top, int sides, double rb, double rt, SolidStyle st,
            double twist = 0, Vec3? reference = null, bool capBottom = true, bool capTop = true)
        {
            sides = Math.Max(3, sides);
            Vec3 axis = (top - bottom).NormalizedOr(Vec3.UnitY);
            Vec3 refDir = reference ?? (Math.Abs(axis.X) < 0.9 ? Vec3.UnitX : Vec3.UnitZ);
            Vec3 e1 = (refDir - axis * Vec3.Dot(refDir, axis)).NormalizedOr(Vec3.UnitZ);
            Vec3 e2 = Vec3.Cross(axis, e1).NormalizedOr(Vec3.UnitX);
            double h = (top - bottom).Length;
            var lo = new Vec3[sides];
            var hi = new Vec3[sides];
            for (int i = 0; i < sides; i++)
            {
                double a = twist + i * DMath.TwoPi / sides;
                DMath.SinCos(a, out double sa, out double ca);
                Vec3 dir = e1 * ca + e2 * sa;
                lo[i] = bottom + dir * rb;
                hi[i] = top + dir * rt;
            }
            double edge = 2 * Math.Max(rb, rt) * DMath.Sin(DMath.Pi / sides);
            double len = Math.Max(h, edge * sides);
            for (int i = 0; i < sides; i++)
            {
                int j = (i + 1) % sides;
                if (rt < 1e-6)
                    Face(b, group, bone, lo[i], lo[j], top, new Vec2(edge * i, 0), new Vec2(edge * (i + 1), 0), new Vec2(edge * (i + 0.5), h), st, len);
                else
                    Quad(b, group, bone, lo[i], lo[j], hi[j], hi[i], edge, h, edge * i, st, len);
            }
            if (capBottom && rb > 1e-6)
                for (int i = 1; i + 1 < sides; i++)
                    Face(b, group, bone, lo[0], lo[i + 1], lo[i], new Vec2(0, 0), new Vec2(rb, rb), new Vec2(rb, 0), st, len);
            if (capTop && rt > 1e-6)
                for (int i = 1; i + 1 < sides; i++)
                    Face(b, group, bone, hi[0], hi[i], hi[i + 1], new Vec2(0, 0), new Vec2(rt, 0), new Vec2(rt, rt), st, len);
            return hi;
        }

        /// <summary>
        /// Half cylinder (vault): a flat base rectangle of width 2r along 'side' and length 2*halfLength along
        /// 'axis', arched towards 'up'. Rounded chest lids, headstones, arched doors and roofs.
        /// </summary>
        public static void Vault(AnatomyBuilder b, int group, int bone, Vec3 center, Vec3 axis, Vec3 up, double halfLength, double radius, int segments, SolidStyle st, double flatten = 1.0)
        {
            Vec3 a = axis.NormalizedOr(Vec3.UnitZ), u = up.NormalizedOr(Vec3.UnitY);
            Vec3 e = Vec3.Cross(u, a).NormalizedOr(Vec3.UnitX);
            segments = Math.Max(2, segments);
            var ra = new Vec3[segments + 1];
            var rb = new Vec3[segments + 1];
            for (int k = 0; k <= segments; k++)
            {
                double th = DMath.Pi * k / segments;
                DMath.SinCos(th, out double sn, out double cs);
                Vec3 d = e * (cs * radius) + u * (sn * radius * flatten);
                ra[k] = center - a * halfLength + d;
                rb[k] = center + a * halfLength + d;
            }
            Vec3 inside = center + u * (radius * flatten * 0.4);
            double arc = DMath.Pi * radius / segments;
            double len = Math.Max(halfLength * 2, DMath.Pi * radius);
            for (int k = 0; k < segments; k++)
            {
                Vec2 q0 = new Vec2(0, arc * k), q1 = new Vec2(halfLength * 2, arc * k), q2 = new Vec2(halfLength * 2, arc * (k + 1)), q3 = new Vec2(0, arc * (k + 1));
                Oriented(b, group, bone, ra[k], rb[k], rb[k + 1], q0, q1, q2, st, len, inside);
                Oriented(b, group, bone, ra[k], rb[k + 1], ra[k + 1], q0, q2, q3, st, len, inside);
                // end caps: fans from the centre of each flat end
                Vec3 ca = center - a * halfLength, cb = center + a * halfLength;
                Oriented(b, group, bone, ca, ra[k], ra[k + 1], new Vec2(radius, 0), new Vec2(radius + (ra[k] - ca).X, (ra[k] - ca).Y), new Vec2(radius + (ra[k + 1] - ca).X, (ra[k + 1] - ca).Y), st, len, inside);
                Oriented(b, group, bone, cb, rb[k], rb[k + 1], new Vec2(radius, 0), new Vec2(radius + (rb[k] - cb).X, (rb[k] - cb).Y), new Vec2(radius + (rb[k + 1] - cb).X, (rb[k + 1] - cb).Y), st, len, inside);
            }
            // flat base
            Oriented(b, group, bone, ra[0], rb[0], rb[segments], new Vec2(0, 0), new Vec2(halfLength * 2, 0), new Vec2(halfLength * 2, radius * 2), st, len, inside);
            Oriented(b, group, bone, ra[0], rb[segments], ra[segments], new Vec2(0, 0), new Vec2(halfLength * 2, radius * 2), new Vec2(0, radius * 2), st, len, inside);
        }

        /// <summary>A face of a convex solid wound so that its normal points away from 'inside'.</summary>
        private static PrimitiveDef Oriented(AnatomyBuilder b, int group, int bone, Vec3 p0, Vec3 p1, Vec3 p2, Vec2 u0, Vec2 u1, Vec2 u2, SolidStyle st, double len, Vec3 inside)
        {
            Vec3 n = Vec3.Cross(p1 - p0, p2 - p0);
            Vec3 c = (p0 + p1 + p2) / 3.0;
            if (Vec3.Dot(n, c - inside) < 0) return Face(b, group, bone, p0, p2, p1, u0, u2, u1, st, len);
            return Face(b, group, bone, p0, p1, p2, u0, u1, u2, st, len);
        }

        /// <summary>Thin flat panel (sign boards, lids, planks, blades): a box with a small depth.</summary>
        public static void Panel(AnatomyBuilder b, int group, int bone, Vec3 center, double width, double height, double thickness, SolidStyle st, Quat? rot = null) =>
            Box(b, group, bone, center, new Vec3(width * 0.5, height * 0.5, Math.Max(0.3, thickness * 0.5)), st, rot);
    }
}
