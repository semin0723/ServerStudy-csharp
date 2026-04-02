using Network.DataObject;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Threading;

namespace Network.Entity
{
    /*public class SyncEntity
    {
        private readonly static Dictionary<Type, List<PropertyInfo>> _syncPropertyPerType = new();
        private PropertyData _propertyData = new();
        private List<PropertyInfo> _syncProperties;
        private bool _isSync;

        private Guid _guid;

        public bool IsSync
        {
            get 
            {
                return _isSync;
            }
            set
            {
                _isSync = value;
            }
        }

        private class PropertyData
        {
            public List<string> propertyName { get; set; }
            public List<object?> propertyValue { get; set; }
        }

        private class JsonDeserializeData
        {
            public List<string> propertyName { get; set; }
            public List<JsonElement> propertyValue { get; set; }
        }

        public SyncEntity()
        {
            _guid = Guid.NewGuid();

            _propertyData.propertyName = new List<string>();
            _propertyData.propertyValue = new List<object?>();

            var childType = this.GetType();
            if (!_syncPropertyPerType.TryGetValue(childType, out _syncProperties))
            {
                _syncProperties = childType.GetProperties()
                    .Where(property => Attribute.IsDefined(property, typeof(SyncAttribute)))
                    .ToList();
                _syncPropertyPerType[childType] = _syncProperties;
            }
        }

        public byte[]? Serialize()
        {
            if (_propertyData.propertyValue.Count < 1)
            {
                foreach (var property in _syncProperties)
                {
                    _propertyData.propertyValue.Add(property.GetValue(this));
                }
            }
            else
            {
                for (int i = 0; i < _syncProperties.Count; i++)
                {
                    _propertyData.propertyValue[i] = _syncProperties[i].GetValue(this);
                }
            }


            var bytes = JsonSerializer.SerializeToUtf8Bytes(_propertyData);

            return bytes;
        }

        public void Deserialize(string json)
        {
            var deserializeData = JsonSerializer.Deserialize<JsonDeserializeData>(json);
            for (int i = 0; i < deserializeData.propertyName.Count; i++)
            {
                string propertyName = deserializeData.propertyName[i];
                PropertyInfo? property = GetType().GetProperty(propertyName);
                Type propertyType = property.PropertyType;
                object? convertedValue = JsonSerializer.Deserialize(deserializeData.propertyValue[i].GetRawText(), propertyType);
                property.SetValue(this, convertedValue);
            }
        }
    }*/
}
