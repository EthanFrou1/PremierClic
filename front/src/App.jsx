import React from 'react'
import { BrowserRouter, Routes, Route } from 'react-router-dom'
import Home from './pages/Home'
import Prospects from './pages/Prospects'
import ProspectDetail from './pages/ProspectDetail'
import Unsubscribe from './pages/Unsubscribe'

export default function App(){
  return (
    <BrowserRouter>
      <Routes>
        <Route path="/" element={<Home/>} />
        <Route path="/prospects" element={<Prospects/>} />
        <Route path="/prospects/:id" element={<ProspectDetail/>} />
        <Route path="/unsubscribe/:token" element={<Unsubscribe/>} />
      </Routes>
    </BrowserRouter>
  )
}
