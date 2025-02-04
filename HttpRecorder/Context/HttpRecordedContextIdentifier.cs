using System;
using System.IO;

namespace HttpRecorder.Context
{
    /// <summary>
    /// A utility class that wraps identifiers for retrieving <see cref="HttpRecorderConcurrentContext"/> from
    /// <see cref="HttpRecorderConcurrentContext.GetContext"/>
    /// </summary>
    public class HttpRecordedContextIdentifier : IEquatable<HttpRecordedContextIdentifier>
    {
        /// <inheridoc/>
        public HttpRecordedContextIdentifier(string filePath, string testName)
        {
            Value = Path.Combine(filePath, testName);
        }

        /// <summary>
        /// The underlying value that is used to compare identifiers
        /// </summary>
        public string Value { get; }

        /// <inheritdoc/>
        public bool Equals(HttpRecordedContextIdentifier other)
        {
            if (other is null)
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }

            return Value == other.Value;
        }
        /// <inheritdoc/>
        public override bool Equals(object obj)
        {
            if (obj is null)
            {
                return false;
            }

            if (ReferenceEquals(this, obj))
            {
                return true;
            }

            if (obj.GetType() != GetType())
            {
                return false;
            }

            return Equals((HttpRecordedContextIdentifier)obj);
        }
        /// <inheritdoc/>
        public override int GetHashCode()
        {
            return Value != null ? Value.GetHashCode() : 0;
        }
        /// <inheritdoc/>
        public override string ToString()
        {
            return Value;
        }
    }
}
