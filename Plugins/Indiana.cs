#pragma warning disable VSTHRD200 // Use "Async" suffix for async methods

namespace karesz.Core
{
	// implementation of: 
	// https://github.com/MolnAtt/IndianaKaresz/blob/master/Tanar.cs
	public class IndianaKaresz : Plugin
	{
		public override string LevelName { get => "indiana.txt"; }

		private readonly Random r = new();
		private bool koponya_a_helyen_van = true;

		public override void TANAR_ROBOTJAI()
		{
			// force reset start position
			Robot.PlaceAt("Karesz", 7 + r.Next(-2, 3), 14 + r.Next(-2, 3), rotation: Direction.Right);

			// BALTÁK
			Robot balta1 = Robot.Create("Első balta", startX: 13, startY: 14 - r.Next(-1, 4), startRotation: (Direction)(r.Next(2) * 2 % 4), alt: true);
			balta1.FeladatAsync = async delegate ()
			{
				await Faltol_falig_ingazik(balta1);
				await Kavicsra_parkol(balta1);
			};

			Robot balta2 = Robot.Create("Második balta", startX: 16, startY: 14 - r.Next(-3, 2), startRotation: (Direction)(r.Next(2) * 2 % 4), alt: true);
			balta2.FeladatAsync = async delegate ()
			{
				await Faltol_falig_ingazik(balta2);
				await Kavicsra_parkol(balta2);
			};

			Robot balta3 = Robot.Create("Harmadik balta", startX: 19, startY: 14 - r.Next(-4, 1), startRotation: Direction.Down, alt: true);
			balta3.FeladatAsync = async delegate ()
			{
				await Faltol_falig_ingazik(balta3);
				await Kavicsra_parkol(balta3);
			};
 
			// KŐGOLYÓ
			Robot kőgolyó = Robot.Create("Kőgolyó", 35, 14, Direction.Left, alt: true);
			kőgolyó.FeladatAsync = async delegate ()
			{
				while (koponya_a_helyen_van)
				{
					if (Robot.CurrentLevel[new Vector(33, 14)] != Level.Tile.Yellow)
						koponya_a_helyen_van = false;
					await kőgolyó.VárjAsync();
				}
				while (!kőgolyó.Ki_fog_lépni_a_pályáról())
					await kőgolyó.LépjAsync();
			};
		}

		private async Task Kavicsra_parkol(Robot balta)
		{
			while (!balta.Előtt_fal_van() && !balta.Alatt_van_kavics())
			{
				await balta.LépjAsync();
				if (balta.Előtt_fal_van())
				{
					await balta.ForduljAsync(1);
					await balta.ForduljAsync(1);
				}
			}
		}

		private async Task Faltol_falig_ingazik(Robot balta)
		{
			while (koponya_a_helyen_van)
			{
				while (!balta.Előtt_fal_van())
					await balta.LépjAsync();

				await balta.ForduljAsync(1);
				await balta.ForduljAsync(1);
			}
		}

		public override void Cleanup()
		{
			Robot.Delete("Első balta");
			Robot.Delete("Második balta");
			Robot.Delete("Harmadik balta");
			Robot.Delete("Kőgolyó");
		}
	}
}
