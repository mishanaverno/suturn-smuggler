using System;
using System.Runtime.CompilerServices;
using System.Globalization;
using UnityEngine;
using Unity.Mathematics;

namespace DoublePrecision {
	/*https://docs.unity3d.com/ScriptReference/Vector2.html*/
	[Serializable]
	public struct Vector2d {

		public const double kEpsilon = double.Epsilon;
		public static double kEpsilonNormalSqrt => math.sqrt(double.Epsilon);

		public double2 doubleVector;

		#region Static Properties
		public static Vector2d zero = new Vector2d(0, 0);
		public static Vector2d one = new Vector2d(1, 1);
		public static Vector2d up = new Vector2d(1, 0);
		public static Vector2d down = new Vector2d(0, -1);
		public static Vector2d left = new Vector2d(-1, 0);
		public static Vector2d right = new Vector2d(1, 0);
		public static Vector2d negativeInfinity = new Vector2d(double.NegativeInfinity, double.NegativeInfinity);
		public static Vector2d positiveInfinity = new Vector2d(double.PositiveInfinity, double.PositiveInfinity);

		#endregion

		#region Constructors
		// Extended the properties a little bit more
		// Double Precision
		public Vector2d(double x = 0, double y = 0, double z = 0, double w = 0) { doubleVector = new double2(x, y); }
		public Vector2d(Vector3d _vector) { doubleVector = new double2(_vector.x, _vector.y); }
		public Vector2d(Vector4d _vector) { doubleVector = new double2(_vector.x, _vector.y); }
		public Vector2d(double2 _vector) { doubleVector = new double2(_vector.x, _vector.y); }
		public Vector2d(double3 _vector) { doubleVector = new double2(_vector.x, _vector.y); }
		public Vector2d(double4 _vector) { doubleVector = new double2(_vector.x, _vector.y); }
		// Single Precision
		public Vector2d(float x = 0, float y = 0, float z = 0, float w = 0) { doubleVector = new double2(x, y); }
		public Vector2d(Vector3 _vector) { doubleVector = new double2(_vector.x, _vector.y); }
		public Vector2d(Vector4 _vector) { doubleVector = new double2(_vector.x, _vector.y); }
		public Vector2d(float2 _vector) { doubleVector = new double2(_vector.x, _vector.y); }
		public Vector2d(float3 _vector) { doubleVector = new double2(_vector.x, _vector.y); }
		public Vector2d(float4 _vector) { doubleVector = new double2(_vector.x, _vector.y); }

		#endregion

		#region Properties
		public double magnitude => math.length(doubleVector);
		public Vector2d normalized => new Vector2d(math.normalize(doubleVector));
		public double sqrMagnitude => math.square(magnitude);
		public double this[int index] {
			get {
				switch (index) {
					case 0: return x;
					case 1: return y;
					default: throw new IndexOutOfRangeException("Invalid Vector2d index");
				}
			}
			set {
				switch (index) {
					case 0: x = value; break;
					case 1: y = value; break;
					default: throw new IndexOutOfRangeException("Invalid Vector2d index");
				}
			}
		}
		public double x {
			get => doubleVector.x;
			set => doubleVector = new double2(value, doubleVector.y);
		}
		public double y {
			get => doubleVector.y;
			set => doubleVector = new double2(doubleVector.x, value);
		}

		#endregion

		#region Public Methods
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		///<summary>
		///	Returns true if the given vector is exactly equal to this vector.
		///</summary>
		public override bool Equals(object other) {
			if (other is Vector2d v) return Equals(v);
			return false;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public override int GetHashCode() { return x.GetHashCode() ^ (y.GetHashCode() << 2); }

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		///<summary>
		///	Returns true if the given vector is exactly equal to this vector.
		///</summary>
		public bool Equals(Vector2d other) {
			return double2.Equals(doubleVector, other.doubleVector);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		///<summary>
		///	Makes this vector have a magnitude of 1.
		///</summary>
		public void Normalize() {
			doubleVector = math.normalize(doubleVector);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		///<summary>
		///	Set x and y components of an existing Vector2.
		///</summary>
		public void Set(double newX, double newY) { x = newX; y = newY; }

		///<summary>
		///	Returns a formatted string for this vector.
		///</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public string ToString(string format) {
			return ToString(format, null);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		///<summary>
		///	Returns a formatted string for this vector.
		///</summary>
		public string ToString(string format, IFormatProvider formatProvider) {
			if (string.IsNullOrEmpty(format))
				format = "F2";
			if (formatProvider == null)
				formatProvider = CultureInfo.InvariantCulture.NumberFormat;
			return string.Format("({0}, {1}, {2})", x.ToString(format, formatProvider), y.ToString(format, formatProvider));
		}
		#endregion

		#region Static Methods 

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		///<summary>
		///	Gets the unsigned angle in degrees between from and to.
		///</summary>
		public static double Angle(Vector2d from, Vector2d to) {
			// sqrt(a) * sqrt(b) = sqrt(a * b) -- valid for real numbers
			double denominator = math.sqrt(from.sqrMagnitude * to.sqrMagnitude);
			if (denominator < kEpsilonNormalSqrt) return 0d;

			double dot = math.clamp(Dot(from, to) / denominator, -1d, 1d);
			return math.acos(dot) * Mathd.Rad2Deg;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		///<summary>
		///	Returns a copy of vector with its magnitude clamped to maxLength.
		///</summary>
		public static Vector2d ClampMagnitude(Vector2d vector, double maxLength) {
			double sqrmag = vector.sqrMagnitude;
			if (sqrmag > maxLength * maxLength) {
				double mag = math.sqrt(sqrmag);
				//these intermediate variables force the intermediate result to be
				//of double precision. without this, the intermediate result can be of higher
				//precision, which changes behavior.
				double normalized_x = vector.x / mag;
				double normalized_y = vector.y / mag;
				return new Vector2d(normalized_x * maxLength,
					normalized_y * maxLength);
			}
			return vector;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		///<summary>
		///	Returns the distance between a and b.
		///</summary>
		public static double Distance(Vector2d a, Vector2d b) {
			double diff_x = a.x - b.x;
			double diff_y = a.y - b.y;
			return math.sqrt(diff_x * diff_x + diff_y * diff_y);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		///<summary>
		///	Dot Product of two vectors.
		///</summary>
		public static double Dot(Vector2d lhs, Vector2d rhs) {
			return math.dot(lhs.doubleVector, rhs.doubleVector);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		///<summary>
		///	Linearly interpolates between vectors a and b by t.
		/// The parameter t is clamped to the range [0, 1].
		/// When t = 0 returns a.
		/// When t = 1 return b.
		/// When t = 0.5 returns the midpoint of a and b.
		///</summary>
		public static Vector2d Lerp(Vector2d a, Vector2d b, double t) {
			t = math.clamp(t, 0, 1);

			return new Vector2d(
				a.x + (b.x - a.x) * t,
				a.y + (b.y - a.y) * t
			);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		///<summary>
		///	Linearly interpolates between vectors a and b by t.
		/// When t = 0 returns a.
		/// When t = 1 return b.
		/// When t = 0.5 returns the midpoint of a and b.
		///</summary>
		public static Vector2d LerpUnclamped(Vector2d a, Vector2d b, double t) {
			return new Vector2d(
				a.x + (b.x - a.x) * t,
				a.y + (b.y - a.y) * t
			);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		///<summary>
		///	Returns a vector that is made from the largest components of two vectors
		///</summary>
		public static Vector2d Max(Vector2d lhs, Vector2d rhs) {
			return new Vector2d(math.max(lhs.doubleVector, rhs.doubleVector));
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		///<summary>
		///	Returns a vector that is made from the smallest components of two vectors
		///</summary>
		public static Vector2d Min(Vector2d lhs, Vector2d rhs) {
			return new Vector2d(math.min(lhs.doubleVector, rhs.doubleVector));
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		///<summary>
		///	Moves a point current towards target.
		///</summary>
		public static Vector2d MoveTowards(Vector2d current, Vector2d target, double maxDistanceDelta) {
			// avoid vector ops because current scripting backends are terrible at inlining
			double toVector_x = target.x - current.x;
			double toVector_y = target.y - current.y;

			double sqdist = toVector_x * toVector_x + toVector_y * toVector_y;

			if (sqdist == 0 || (maxDistanceDelta >= 0 && sqdist <= maxDistanceDelta * maxDistanceDelta))
				return target;
			var dist = math.sqrt(sqdist);

			return new Vector2d(
				current.x + toVector_x / dist * maxDistanceDelta,
				current.y + toVector_y / dist * maxDistanceDelta
				);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		///<summary>
		///	Returns the 2D vector perpendicular to this 2D vector. The result is always rotated 90-degrees in a counter-clockwise direction for a 2D coordinate system where the positive Y axis goes up.
		///</summary>
		public static Vector2d Perpendicular(Vector2d inDirection) {
			return new Vector2d(-inDirection.y, inDirection.x);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		///<summary>
		///	Reflects a vector off the surface defined by a normal.
		///</summary>
		public static Vector2d Reflect(Vector2d inDirection, Vector2d normal) {
			return new Vector2d(math.reflect(inDirection.doubleVector, normal.doubleVector));
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		///<summary>
		///	Multiplies two vectors component-wise.
		/// Every component in the result is a component of a multiplied by the same component of b.
		///</summary>
		public static Vector2d Scale(Vector2d a, Vector2d b) {
			return new Vector2d(a.doubleVector * b.doubleVector);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		///<summary>
		/// Gets the signed angle in degrees between from and to.
		///</summary>
		public static double SignedAngle(Vector2d from, Vector2d to) {
			double angle = math.degrees(math.atan2(to.x, to.y) - math.atan2(from.x, from.y));
			// Normalize to [-180, 180]
			return math.select(angle, angle + 360d, angle < -180d) % 360d;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		///<summary>
		///	Gradually changes a vector towards a desired goal over time.
		///</summary>
		public static Vector2d SmoothDamp(Vector2d current, Vector2d target, ref Vector2d currentVelocity, double smoothTime, double maxSpeed = double.PositiveInfinity, double deltaTime = 1 / 50d) {

			double output_x = 0f;
			double output_y = 0f;

			// Based on Game Programming Gems 4 Chapter 1.10
			smoothTime = Math.Max(0.0001F, smoothTime);
			double omega = 2F / smoothTime;

			double x = omega * deltaTime;
			double exp = 1F / (1F + x + 0.48F * x * x + 0.235F * x * x * x);

			double change_x = current.x - target.x;
			double change_y = current.y - target.y;
			Vector2d originalTo = target;

			// Clamp maximum speed
			double maxChange = maxSpeed * smoothTime;

			double maxChangeSq = maxChange * maxChange;
			double sqrmag = change_x * change_x + change_y * change_y;
			if (sqrmag > maxChangeSq) {
				var mag = math.sqrt(sqrmag);
				change_x = change_x / mag * maxChange;
				change_y = change_y / mag * maxChange;
			}

			target.x = current.x - change_x;
			target.y = current.y - change_y;

			double temp_x = (currentVelocity.x + omega * change_x) * deltaTime;
			double temp_y = (currentVelocity.y + omega * change_y) * deltaTime;

			currentVelocity.x = (currentVelocity.x - omega * temp_x) * exp;
			currentVelocity.y = (currentVelocity.y - omega * temp_y) * exp;

			output_x = target.x + (change_x + temp_x) * exp;
			output_y = target.y + (change_y + temp_y) * exp;

			// Prevent overshooting
			double origMinusCurrent_x = originalTo.x - current.x;
			double origMinusCurrent_y = originalTo.y - current.y;
			double outMinusOrig_x = output_x - originalTo.x;
			double outMinusOrig_y = output_y - originalTo.y;

			if (origMinusCurrent_x * outMinusOrig_x + origMinusCurrent_y * outMinusOrig_y > 0) {
				output_x = originalTo.x;
				output_y = originalTo.y;

				currentVelocity.x = (output_x - originalTo.x) / deltaTime;
				currentVelocity.y = (output_y - originalTo.y) / deltaTime;
			}

			return new Vector2d(output_x, output_y);
		}


		#endregion


		#region Operators
		// Adds two vectors.
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Vector2d operator +(Vector2d a, Vector2d b) => new Vector2d(a.doubleVector + b.doubleVector);

		// Subtracts one vector from another.
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Vector2d operator -(Vector2d a, Vector2d b) => new Vector2d(a.doubleVector - b.doubleVector);

		// Negates a vector.
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Vector2d operator -(Vector2d a) { return new Vector2d(-a.x, -a.y); }

		// Multiplies a vector by a number.
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Vector2d operator *(Vector2d a, double d) => new Vector2d(a.doubleVector * d);

		// Multiplies a vector by a number.
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Vector2d operator *(double d, Vector2d a) => new Vector2d(a.doubleVector * d);

		// Divides a vector by a number.
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Vector2d operator /(Vector2d a, double d) => new Vector2d(a.doubleVector / d);

		// Returns true if the vectors are equal.
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator ==(Vector2d lhs, Vector2d rhs) => math.Equals(lhs, rhs);

		// Returns true if vectors are different.
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator !=(Vector2d lhs, Vector2d rhs) => !math.Equals(lhs, rhs);

		// Cast to float2 (explicit)
		public static explicit operator Vector2(Vector2d v) => new Vector2((float)v.x, (float)v.y);

		// Cast from float2 (implicit)
		public static implicit operator Vector2d(Vector2 v) => new Vector2d(v.x, v.y);
	
		#endregion

	}
}