﻿using System.Text.Json.Serialization;

namespace LANCommander.Data.Models
{
    public abstract class BaseTaxonomyModel : BaseModel
    {
        public string Name { get; set; }
        [JsonIgnore]
        public virtual ICollection<Game> Games { get; set; }
    }
}
