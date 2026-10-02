using System;
using UnityEngine;

namespace ToolForDialogue._Project.Dialogue.Runtime.Core
{
    /// <summary>
    /// A Unity-serializable, rename-safe identifier for authored dialogue data.
    /// </summary>
    [Serializable]
    public struct SerializableGuid : IEquatable<SerializableGuid>
    {
        [SerializeField] private string _value;

        public static SerializableGuid empty => default;

        public string value => _value ?? string.Empty;

        public bool isValid => Guid.TryParseExact(_value, "N", out _);

        private SerializableGuid(Guid guid)
        {
            _value = guid.ToString("N");
        }

        public static SerializableGuid NewGuid()
        {
            return new SerializableGuid(Guid.NewGuid());
        }

        public static bool TryParse(string input, out SerializableGuid result)
        {
            if (Guid.TryParseExact(input, "N", out var guid))
            {
                result = new SerializableGuid(guid);
                return true;
            }

            result = empty;
            return false;
        }

        public bool Equals(SerializableGuid other)
        {
            return string.Equals(value, other.value, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is SerializableGuid other && Equals(other);
        }

        public override int GetHashCode()
        {
            return StringComparer.Ordinal.GetHashCode(value);
        }

        public override string ToString()
        {
            return value;
        }

        public static bool operator ==(SerializableGuid left, SerializableGuid right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(SerializableGuid left, SerializableGuid right)
        {
            return !left.Equals(right);
        }
    }
}
