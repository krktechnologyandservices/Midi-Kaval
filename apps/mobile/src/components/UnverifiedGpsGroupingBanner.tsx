import React from 'react';

import {Pressable, StyleSheet, Text, View} from 'react-native';



type Props = {

  onDismiss: () => void;

};



export function UnverifiedGpsGroupingBanner({

  onDismiss,

}: Props): React.JSX.Element {

  return (

    <View style={styles.banner} accessibilityRole="text">

      <Text style={styles.text}>

        Some visits need landmark capture before they can be grouped

      </Text>

      <Pressable

        onPress={onDismiss}

        accessibilityRole="button"

        accessibilityLabel="Dismiss grouping warning">

        <Text style={styles.dismiss}>Dismiss</Text>

      </Pressable>

    </View>

  );

}



const styles = StyleSheet.create({

  banner: {

    marginBottom: 12,

    paddingVertical: 10,

    paddingHorizontal: 12,

    backgroundColor: '#FFFAEB',

    borderLeftWidth: 4,

    borderLeftColor: '#B54708',

    borderRadius: 8,

  },

  text: {

    fontSize: 13,

    color: '#101828',

    marginBottom: 8,

  },

  dismiss: {

    fontSize: 13,

    color: '#175CD3',

    fontWeight: '600',

  },

});


