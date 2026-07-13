import React from 'react';
import { createTheme, ThemeProvider } from '@material-ui/core/styles';
import CssBaseline from '@material-ui/core/CssBaseline';
import { lighten } from '@material-ui/core/styles/colorManipulator';

const hisHopeTheme = createTheme({
  palette: {
    type: 'light',
    primary: {
      main: '#1a73e8',
      light: '#4a9af5',
      dark: '#0d47a1',
      contrastText: '#ffffff',
    },
    secondary: {
      main: '#00bcd4',
      light: '#4dd0e1',
      dark: '#0097a7',
      contrastText: '#ffffff',
    },
    background: {
      default: '#f5f7fa',
      paper: '#ffffff',
    },
    text: {
      primary: '#1a2332',
      secondary: '#5f6b7a',
    },
    error: {
      main: '#d32f2f',
    },
    warning: {
      main: '#f9a825',
    },
    success: {
      main: '#2e7d32',
    },
    info: {
      main: '#0288d1',
    },
  },
  typography: {
    fontFamily: [
      '-apple-system',
      'BlinkMacSystemFont',
      '"Segoe UI"',
      'Roboto',
      '"Helvetica Neue"',
      'Arial',
      'sans-serif',
    ].join(','),
    h1: {
      fontSize: '2rem',
      fontWeight: 600,
      color: '#1a2332',
    },
    h2: {
      fontSize: '1.5rem',
      fontWeight: 600,
      color: '#1a2332',
    },
    h3: {
      fontSize: '1.25rem',
      fontWeight: 600,
      color: '#1a2332',
    },
    body1: {
      fontSize: '0.875rem',
      color: '#1a2332',
    },
    body2: {
      fontSize: '0.75rem',
      color: '#5f6b7a',
    },
  },
  shape: {
    borderRadius: 8,
  },
  overrides: {
    MuiAppBar: {
      colorPrimary: {
        backgroundColor: '#ffffff',
        color: '#1a2332',
        boxShadow: '0 1px 3px rgba(0,0,0,0.08)',
      },
    },
    MuiDrawer: {
      paper: {
        backgroundColor: '#1a2332',
        color: '#ffffff',
        borderRight: 'none',
      },
    },
    MuiButton: {
      root: {
        textTransform: 'none',
        fontWeight: 500,
      },
      containedPrimary: {
        '&:hover': {
          backgroundColor: '#1557b0',
        },
      },
    },
    MuiCard: {
      root: {
        boxShadow: '0 1px 3px rgba(0,0,0,0.08), 0 1px 2px rgba(0,0,0,0.06)',
        borderRadius: 8,
      },
    },
    MuiTableCell: {
      root: {
        borderBottom: '1px solid #e8eaed',
      },
      head: {
        fontWeight: 600,
        color: '#5f6b7a',
      },
    },
    MuiChip: {
      root: {
        borderRadius: 4,
      },
    },
  },
  props: {
    MuiAppBar: {
      elevation: 0,
    },
    MuiCard: {
      elevation: 0,
    },
  },
});

type Props = {
  children: React.ReactNode;
};

export const Root = ({ children }: Props) => {
  return (
    <ThemeProvider theme={hisHopeTheme}>
      <CssBaseline />
      {children}
    </ThemeProvider>
  );
};
