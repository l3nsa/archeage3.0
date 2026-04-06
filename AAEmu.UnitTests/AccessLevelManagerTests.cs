using Xunit;
using Newtonsoft.Json;
using System.Collections.Generic;

namespace AAEmu.UnitTests
{
    public class AccessLevelManagerTests
    {
        [Fact]
        public void DeserializeAccessLevels_ValidJson_ReturnsDictionary()
        {
            var json = "{\"help\": 0, \"spawn\": 100, \"kill\": 100}";
            var result = JsonConvert.DeserializeObject<Dictionary<string, int>>(json);
            
            Assert.NotNull(result);
            Assert.Equal(3, result.Count);
            Assert.Equal(0, result["help"]);
            Assert.Equal(100, result["spawn"]);
        }

        [Fact]
        public void DeserializeAccessLevels_EmptyJson_ReturnsEmptyDictionary()
        {
            var json = "{}";
            var result = JsonConvert.DeserializeObject<Dictionary<string, int>>(json);
            
            Assert.NotNull(result);
            Assert.Empty(result);
        }

        [Fact]
        public void DeserializeAccessLevels_InvalidJson_ThrowsException()
        {
            var json = "invalid json";
            Assert.Throws<JsonReaderException>(() => 
                JsonConvert.DeserializeObject<Dictionary<string, int>>(json));
        }
    }
}
