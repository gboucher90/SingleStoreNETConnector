#if !BASELINE
using SingleStoreConnector.Protocol;
using SingleStoreConnector.Protocol.Serialization;
#endif

namespace SideBySide;

public class CharacterSetTests : IClassFixture<DatabaseFixture>
{
	public CharacterSetTests(DatabaseFixture database)
	{
		m_database = database;
	}

#if !BASELINE
	[Fact]
	public void MaxLength()
	{
		using var reader = m_database.Connection.ExecuteReader(@"select coll.ID, cs.MAXLEN from information_schema.collations coll inner join information_schema.character_sets cs using(CHARACTER_SET_NAME);");
		while (reader.Read())
		{
			var characterSet = (CharacterSet) reader.GetInt32(0);
			var maxLength = reader.GetInt32(1);

			Assert.Equal(maxLength, ProtocolUtility.GetBytesPerCharacter(characterSet));
		}
	}
#endif

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public void IllegalMixOfCollations(bool reopenConnection)
	{
		var csb = AppConfig.CreateConnectionStringBuilder();
		csb.AllowUserVariables = true;
		using var connection = new MySqlConnection(csb.ConnectionString);
		connection.Open();
		connection.Execute(@"
DROP TABLE IF EXISTS mix_collations;
CREATE TABLE mix_collations (
id int(11) NOT NULL AUTO_INCREMENT,
test_col varchar(10) DEFAULT NULL,
PRIMARY KEY (id),
KEY ix_test (test_col)
);
INSERT INTO mix_collations (test_col)
VALUES ('a'), ('b'), ('c'), ('d'), ('e'), ('f'), ('g'), ('h'), ('i'), ('j');");

		if (reopenConnection)
		{
			connection.Close();
			connection.Open();
		}

		using var reader = connection.ExecuteReader(@"
		SET @param = 'B';
		SELECT * FROM mix_collations a WHERE a.test_col = @param");
		Assert.True(reader.Read());
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public void CollationConnection(bool reopenConnection)
	{
		var csb = AppConfig.CreateConnectionStringBuilder();
#if BASELINE
		csb.CharacterSet = "utf8mb4";
#endif
		using var connection = new MySqlConnection(csb.ConnectionString);
		connection.Open();

		if (reopenConnection)
		{
			connection.Close();
			connection.Open();
		}

		var collation = connection.Query<string>(@"select @@collation_connection;").Single();
		var expected = connection.ServerVersion.StartsWith("8.0") ? "utf8mb4_0900_ai_ci" : "utf8mb4_general_ci";
		Assert.Equal(expected, collation);
	}

	readonly DatabaseFixture m_database;
}
