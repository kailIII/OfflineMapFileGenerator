﻿namespace com.atgardner.OMFG.packagers
{
    using com.atgardner.OMFG.Properties;
    using com.atgardner.OMFG.tiles;
    using com.atgardner.OMFG.utils;
    using System.IO;
    using System.Security;
    using System.Text;
    using System.Threading.Tasks;
    using System.Data;

    class OruxPackager : SQLitePackager
    {
        private const int Tile_Size = 256;
        private readonly string targetPath;
        private readonly Map map;

        private string MapName
        {
            get
            {
                return SecurityElement.Escape(Path.GetFileNameWithoutExtension(targetPath));
            }
        }

        protected override string TABLE_DDL
        {
            get { return "CREATE TABLE IF NOT EXISTS tiles (x int, y int, z int, image blob, PRIMARY KEY (x,y,z))"; }
        }

        protected override string INDEX_DDL
        {
            get { return "CREATE INDEX IF NOT EXISTS IND on tiles (x,y,z)"; }
        }

        protected override string INSERT_SQL
        {
            get { return "INSERT or IGNORE INTO tiles (x,y,z,image) VALUES (@x, @y, @z, @image)"; }
        }

        public OruxPackager(string targetPath, Map map)
            : base(targetPath)
        {
            this.targetPath = targetPath;
            this.map = map;
        }

        protected override string GetDbFileName(string path)
        {
            var dbFileName = Path.Combine(path, "OruxMapsImages.db");
            if (File.Exists(dbFileName))
            {
                File.Delete(dbFileName);
            }

            return dbFileName;
        }

        public override async Task AddTile(Tile tile)
        {
            if (tile == null)
            {
                return;
            }

            var layer = map[tile.Zoom];
            var bounds = layer.Bounds;
            var command = Connection.CreateCommand();
            command.CommandText = INSERT_SQL;
            AddParameter(command, DbType.Int32, "x", tile.X - bounds.MinX);
            AddParameter(command, DbType.Int32, "y", tile.Y - bounds.MinY);
            AddParameter(command, DbType.Int32, "z", tile.Zoom);
            AddParameter(command, DbType.Binary, "image", tile.Image);
            await command.ExecuteNonQueryAsync();
        }

        protected override async Task UpdateTileMetaInfo()
        {
            var sb = new StringBuilder();
            var zoomLevels = map.ZoomLevels;
            foreach (var zoom in zoomLevels)
            {
                var layer = map[zoom];
                var bounds = layer.Bounds;
                var tl = bounds.TL;
                var tlLongitude = tl.Longitude == 180D ? -tl.Longitude.Degrees : tl.Longitude.Degrees;
                var br = bounds.BR;
                var width = bounds.Width * Tile_Size;
                var height = bounds.Height * Tile_Size;
                var xMax = (width + Tile_Size - 1) / Tile_Size;
                var yMax = (height + Tile_Size - 1) / Tile_Size;
                sb.AppendFormat(Resources.OruxLayerTemplate, MapName, zoom, xMax, yMax, height, width, br.Latitude.Degrees, tl.Latitude.Degrees, tlLongitude, br.Longitude.Degrees);
            }

            var contents = string.Format(Resources.OruxMapTemplate, MapName, sb);
            var otrkFileName = Path.ChangeExtension(Path.Combine(targetPath, MapName), ".otrk2.xml");
            await Task.Factory.StartNew(() => File.WriteAllText(otrkFileName, contents));
        }
    }
}
