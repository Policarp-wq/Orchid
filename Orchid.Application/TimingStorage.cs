using Orchid.Core;

namespace Orchid.Application;

// Храним ивенты - в коннкретноый момет времени нажали клавишу или отжали
public class TimingStorage(ITimeProvider timeProvider) : ITimingStorage
{
    // Из опций брать
    // Чтобы аккорды нормально считались, однако арпеджио могут обосраться и стать аккордами - нужны замеры
    // Стаккадо и подобные тоже хуй пойми что
    // На литсочке надо порисовать как мы будем потом восстанавливать хоронлогию
    // + вынести всё в конфиг файл
    private const int BufferSizeMs = 20;
    private readonly Stack<TimeBin> _timeBins = new();
    public Task SaveTimingAsync(KeyPress timing, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var time = timeProvider.GetCurrentTimeMilliseconds();
        if(_timeBins.Count == 0 || time - _timeBins.Peek().StartTime >= BufferSizeMs)
        {
            _timeBins.Push(new TimeBin(time, BufferSizeMs,  [ timing ]));
        }
        else
        {
            var currentBin = _timeBins.Pop();
            var updatedTones = currentBin.Timings.Append(timing).ToArray();
            _timeBins.Push(currentBin with { Timings = updatedTones });
        }
        return Task.CompletedTask;
    }

    public IEnumerable<KeyPressTiming> GetTimings() => _timeBins
        .Reverse()
        .SelectMany(bin => bin.Timings.Select(timing => new KeyPressTiming(timing, bin.StartTime)));
    
    private record TimeBin(long StartTime, long Duration, KeyPress[] Timings);
}
