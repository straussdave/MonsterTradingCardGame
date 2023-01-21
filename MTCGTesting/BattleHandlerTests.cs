using MonsterTradingCardGame;
using MonsterTradingCardGame.Models;

namespace MonsterTradingCardGame.MTCGTesting.BattleTests
{
    public class BattleHandlerTests
    {
        private BattleManager bm { get; set; } = null!;
        [SetUp]
        public void Setup()
        {
            bm = new BattleManager();
        }

        [Test]
        public void GetElementTest()
        {
            Card card = new Card();
            card.Name = "FireElf";
            card.Damage = 25;

            var element = bm.GetElement(card);

            Assert.That(element, Is.EqualTo("Fire"));
        }
        [Test]
        public void GetTypeTest()
        {
            Card card = new Card();
            card.Name = "FireElf";
            card.Damage = 25;

            var type = bm.GetType(card, "Fire");

            Assert.That(type, Is.EqualTo("Elf"));
        }
        [Test]
        public void CalculateDamageTest()
        {
            Card card1 = new Card();
            card1.Name = "FireElf";
            card1.Damage = 25;

            Card card2 = new Card();
            card2.Name = "WaterSpell";
            card2.Damage = 15;

            Card winner = new Card();
            winner = bm.CalculateDamage(card1, card2);

            Assert.That(winner.Name, Is.EqualTo(card2.Name));
        }
        [Test]
        public void EnqueueTestBattleId()
        {
            Player player = new Player();
            List<string> battleLog = new List<string>();
            battleLog.Add("0");
            bm.BattleHistory.Add(battleLog);

            int battleId = bm.Enqueue(player);

            Assert.That(battleId, Is.EqualTo(1));
        }
        [Test]
        public void EnqueueTestQueue()
        {
            Player player = new Player();
            bm.Enqueue(player);
            Assert.That(bm.WaitingPlayers.Count, Is.EqualTo(1));
        }
        [Test]
        public void BattleTest()
        {
            Player player1 = new Player();
            player1.Name = "testperson";
            player1.Deck = new List<Card>();
            Card player1Card = new Card();
            player1Card.Name = "FireElf";
            player1Card.Damage = 10;
            player1Card.Id = "67f9048f-99b8-4ae4-b866-d8008d00c53d";
            Player player2 = new Player();
            player2.Name = "max mustermann";
            player2.Deck = new List<Card>(); 
            Card player2Card = new Card();
            player2Card.Name = "WaterSpell";
            player2Card.Damage = 10;
            player2Card.Id = "70962948-2bf7-44a9-9ded-8c68eeac7793";

            player1.Deck.Add(player1Card);
            player1.Deck.Add(player1Card);
            player1.Deck.Add(player1Card);
            player1.Deck.Add(player1Card);
            player2.Deck.Add(player2Card);
            player2.Deck.Add(player2Card);
            player2.Deck.Add(player2Card);
            player2.Deck.Add(player2Card);

            bm.Enqueue(player1);
            bm.Enqueue(player2);
            bm.Battle();

            List<string> battleLog = bm.BattleHistory.ElementAt(0);
            string winner = battleLog.Last().ToString().Substring(8);

            Assert.That(winner, Is.EqualTo("max mustermann\n"));
        }
    }
}
