import './globals.css'

export default function RootLayout({
  children,
}: Readonly<{
  children: React.ReactNode
}>) {
  return (
    <html lang="en">
      <head>
        <title>LearnOS AI</title>
        <meta name="description" content="Smart learning platform" />
        <link rel="manifest" href="/manifest.webmanifest" />
      </head>
      <body>{children}</body>
    </html>
  )
}
