import {DateTimePickerAndroid} from '@react-native-community/datetimepicker';

// @react-native-community/datetimepicker's declarative <DateTimePicker> crashes on
// Android with "Cannot read property 'dismiss' of undefined" when the native dialog
// is still closing while the component re-renders/unmounts (easy to hit on screens
// with several pickers, like court sitting date fields). The library's own docs
// recommend the imperative API on Android instead of conditionally mounting the
// component — this wrapper is the single place that calls it.
export function openAndroidDatePicker(options: {
  value: Date;
  mode: 'date' | 'datetime';
  minimumDate?: Date;
  onChange: (date: Date) => void;
}): void {
  if (options.mode === 'date') {
    DateTimePickerAndroid.open({
      value: options.value,
      mode: 'date',
      minimumDate: options.minimumDate,
      onChange: (_event, date) => {
        if (date) {
          options.onChange(date);
        }
      },
    });
    return;
  }

  // Android has no combined "datetime" native dialog (unlike iOS) — the imperative
  // API only supports separate 'date' and 'time' modes, so chain them and merge the
  // results into a single Date.
  DateTimePickerAndroid.open({
    value: options.value,
    mode: 'date',
    minimumDate: options.minimumDate,
    onChange: (_event, pickedDate) => {
      if (!pickedDate) {
        return;
      }

      DateTimePickerAndroid.open({
        value: options.value,
        mode: 'time',
        onChange: (_timeEvent, pickedTime) => {
          if (!pickedTime) {
            return;
          }

          const combined = new Date(pickedDate);
          combined.setHours(pickedTime.getHours(), pickedTime.getMinutes(), 0, 0);
          options.onChange(combined);
        },
      });
    },
  });
}
