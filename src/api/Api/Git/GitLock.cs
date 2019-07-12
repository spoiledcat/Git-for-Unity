using System;
using System.Globalization;
using Unity.VersionControl.Git;
using Unity.VersionControl.Git.Json;
using Unity.VersionControl.Git.NiceIO;

namespace Unity.VersionControl.Git
{
    [Serializable]
    public struct GitLock
    {
        public static GitLock Default = new GitLock();

        public string id;
        public string path;
        public GitUser owner;
        [NotSerialized] public string lockedAtString;
        private string LockedAtString { get { return lockedAtString != null ? lockedAtString : String.Empty; } }
        public DateTimeOffset locked_at
        {
            get
            {
                DateTimeOffset dt;
                if (!DateTimeOffset.TryParseExact(LockedAtString.ToEmptyIfNull(), Json.DateTimeFormatConstants.Iso8601Formats,
                        CultureInfo.InvariantCulture, Json.DateTimeFormatConstants.DateTimeStyle, out dt))
                {
                    locked_at = DateTimeOffset.MinValue;
                    return DateTimeOffset.MinValue;
                }
                return dt;
            }
            set
            {
                lockedAtString = value.ToUniversalTime().ToString(Json.DateTimeFormatConstants.Iso8601FormatZ, CultureInfo.InvariantCulture);
            }
        }
        [NotSerialized] public string ID => id ?? String.Empty;
        [NotSerialized] public NPath Path => path?.ToNPath() ?? NPath.Default;
        [NotSerialized] public GitUser Owner => owner;
        [NotSerialized] public DateTimeOffset LockedAt => locked_at;

        public GitLock(string id, NPath path, GitUser owner, DateTimeOffset locked_at)
        {
            this.id = id;
            this.path = path.IsInitialized ? path.ToString() : null;
            this.owner = owner;
            this.lockedAtString = locked_at.ToUniversalTime().ToString(Json.DateTimeFormatConstants.Iso8601FormatZ, CultureInfo.InvariantCulture);
        }

        public override bool Equals(object other)
        {
            if (other is GitLock)
                return Equals((GitLock)other);
            return false;
        }

        public bool Equals(GitLock other)
        {
            return this == other;
        }

        public override int GetHashCode()
        {
            int hash = 17;
            hash = hash * 23 + ID.GetHashCode();
            hash = hash * 23 + Path.GetHashCode();
            hash = hash * 23 + owner.GetHashCode();
            hash = hash * 23 + locked_at.GetHashCode();
            return hash;
        }

        public static bool operator ==(GitLock lhs, GitLock rhs)
        {
            return lhs.ID == rhs.ID && lhs.Path == rhs.Path && lhs.owner == rhs.owner && lhs.locked_at == rhs.locked_at;
        }

        public static bool operator !=(GitLock lhs, GitLock rhs)
        {
            return !(lhs == rhs);
        }
        public override string ToString()
        {
            return $"{{ID:{ID}, path:{Path}, owner:{{{owner}}}, locked_at:'{locked_at}'}}";
        }
    }
}
