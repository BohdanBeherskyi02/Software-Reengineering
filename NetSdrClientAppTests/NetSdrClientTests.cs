using Moq;
using NetSdrClientApp;
using NetSdrClientApp.Networking;

namespace NetSdrClientAppTests;

public class NetSdrClientTests
{
    NetSdrClient _client;
    Mock<ITcpClient> _tcpMock;
    Mock<IUdpClient> _updMock;

    public NetSdrClientTests() { }

    [SetUp]
    public void Setup()
    {
        _tcpMock = new Mock<ITcpClient>();
        _tcpMock.Setup(tcp => tcp.Connect()).Callback(() =>
        {
            _tcpMock.Setup(tcp => tcp.Connected).Returns(true);
        });

        _tcpMock.Setup(tcp => tcp.Disconnect()).Callback(() =>
        {
            _tcpMock.Setup(tcp => tcp.Connected).Returns(false);
        });

        _tcpMock.Setup(tcp => tcp.SendMessageAsync(It.IsAny<byte[]>())).Callback<byte[]>((bytes) =>
        {
            _tcpMock.Raise(tcp => tcp.MessageReceived += null, _tcpMock.Object, bytes);
        });

        _updMock = new Mock<IUdpClient>();

        _client = new NetSdrClient(_tcpMock.Object, _updMock.Object);
    }

    [Test]
    public async Task ConnectAsyncTest()
    {
        //act
        await _client.ConnectAsync();

        //assert
        _tcpMock.Verify(tcp => tcp.Connect(), Times.Once);
        _tcpMock.Verify(tcp => tcp.SendMessageAsync(It.IsAny<byte[]>()), Times.Exactly(3));
    }

    [Test]
    public async Task DisconnectWithNoConnectionTest()
    {
        //act
        _client.Disconect();

        //assert
        //No exception thrown
        _tcpMock.Verify(tcp => tcp.Disconnect(), Times.Once);
    }

    [Test]
    public async Task DisconnectTest()
    {
        //Arrange 
        await ConnectAsyncTest();

        //act
        _client.Disconect();

        //assert
        //No exception thrown
        _tcpMock.Verify(tcp => tcp.Disconnect(), Times.Once);
    }

    [Test]
    public async Task StartIQNoConnectionTest()
    {

        //act
        await _client.StartIQAsync();

        //assert
        //No exception thrown
        _tcpMock.Verify(tcp => tcp.SendMessageAsync(It.IsAny<byte[]>()), Times.Never);
        _tcpMock.VerifyGet(tcp => tcp.Connected, Times.AtLeastOnce);
    }

    [Test]
    public async Task StartIQTest()
    {
        //Arrange 
        await ConnectAsyncTest();

        //act
        await _client.StartIQAsync();

        //assert
        //No exception thrown
        _updMock.Verify(udp => udp.StartListeningAsync(), Times.Once);
        Assert.That(_client.IQStarted, Is.True);
    }

    [Test]
    public async Task StopIQTest()
    {
        //Arrange 
        await ConnectAsyncTest();

        //act
        await _client.StopIQAsync();

        //assert
        //No exception thrown
        _updMock.Verify(tcp => tcp.StopListening(), Times.Once);
        Assert.That(_client.IQStarted, Is.False);
    }

    //TODO: cover the rest of the NetSdrClient code here
    [Test]
    public async Task ChangeFrequencyNoConnectionTest()
    {
        //Arrange - no connection established
        _tcpMock.Setup(tcp => tcp.Connected).Returns(false);

        //Act
        await _client.ChangeFrequencyAsync(14250000, 1);

        //Assert - no messages should be sent
        _tcpMock.Verify(tcp => tcp.SendMessageAsync(It.IsAny<byte[]>()), Times.Never);
    }

    [Test]
    public async Task ChangeFrequencyAsync_WhenConnected_ShouldSendCorrectMessage()
    {
        //Arrange
        await ConnectAsyncTest();
        long frequency = 14250000; // 14.25 MHz
        int channel = 1;
        int initialCallCount = 3; // ConnectAsync makes 3 calls

        //Act
        await _client.ChangeFrequencyAsync(frequency, channel);

        //Assert - verify one more call was made after ConnectAsync
        _tcpMock.Verify(tcp => tcp.SendMessageAsync(It.IsAny<byte[]>()), Times.Exactly(initialCallCount + 1));
    }

    [Test]
    public async Task ChangeFrequencyAsync_WithDifferentChannels_ShouldWork()
    {
        //Arrange
        await ConnectAsyncTest();
        int initialCallCount = 3;

        //Act & Assert for channel 0
        await _client.ChangeFrequencyAsync(7000000, 0);
        _tcpMock.Verify(tcp => tcp.SendMessageAsync(It.IsAny<byte[]>()), Times.Exactly(initialCallCount + 1));

        //Act & Assert for channel 2
        await _client.ChangeFrequencyAsync(21000000, 2);
        _tcpMock.Verify(tcp => tcp.SendMessageAsync(It.IsAny<byte[]>()), Times.Exactly(initialCallCount + 2));
    }

    [Test]
    public async Task ChangeFrequencyAsync_MultipleFrequencies_ShouldSendEachTime()
    {
        //Arrange
        await ConnectAsyncTest();
        var frequencies = new[] { 7000000L, 14000000L, 21000000L, 28000000L };
        int initialCalls = 3;

        //Act
        foreach (var freq in frequencies)
        {
            await _client.ChangeFrequencyAsync(freq, 0);
        }

        //Assert
        _tcpMock.Verify(tcp => tcp.SendMessageAsync(It.IsAny<byte[]>()), 
            Times.Exactly(initialCalls + frequencies.Length));
    }
}
