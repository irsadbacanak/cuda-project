var streamServer  = new StreamServer(8888);
var inputReceiver = new InputReceiver(8889);

Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    inputReceiver.Stop();
    streamServer.Stop();
};

inputReceiver.Start();  // arka planda dinler
streamServer.Start();   // ana thread'i bloklar
